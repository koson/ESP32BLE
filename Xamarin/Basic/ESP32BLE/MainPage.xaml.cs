using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using nexus.protocols.ble;
using nexus.protocols.ble.scan;
using Xamarin.Forms;
using nexus.protocols.ble.gatt.adopted;
using nexus.protocols.ble.gatt;

namespace ESP32BLE
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(true)]
    public partial class MainPage : ContentPage
    {
        IBluetoothLowEnergyAdapter Adapter { get; set; }
        IBleGattServerConnection GattServer { get; set; }
        Guid serviceGuid = new Guid("ABCD1234-0aaa-467a-9538-01f0652c74e8");
        Guid sliderGuid = new Guid("ABCD1235-0aaa-467a-9538-01f0652c74e8");
        Guid buttonGuid = new Guid("ABCD1236-0aaa-467a-9538-01f0652c74e8");

        int oldSliderValue = 0;

        IDisposable notifyHandler;
        bool Pulsante = false;

        Color oldColor;

        //public ICommand cmdConnect { get; }
        //public ICommand cmdDisconnect { get; }

        public MainPage( IBluetoothLowEnergyAdapter adapter )
        {
            Adapter = adapter;

            // i command così non si bindano... Devono essere messi all'interno di una INotifyPropertyChanged...
            //cmdConnect = new Command(async () => await Connect(true));
            //cmdDisconnect = new Command(async () => await Connect(false));

            InitializeComponent();
            oldColor = lblTitolo.BackgroundColor;

            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                AggiornaStato();
                return true; // True = Repeat again, False = Stop the timer
            });
        }

        async void AggiornaStato( )
        {
            bool connesso = false;
            try
            {
                if (GattServer != null)
                {
                    connesso = GattServer.State == ConnectionState.Connected;

                    if (connesso)
                    {
                        // Spedisce il valore dello slider
                        int valore = Convert.ToInt32(slValore.Value);
                        byte[] bufferDaSpedire = BitConverter.GetBytes(valore);

                        var result = await GattServer.WriteCharacteristicValue(
                            serviceGuid, sliderGuid,
                            bufferDaSpedire
                        );
                    }
                }
            }
            catch (Exception errore)
            {
                Debug.WriteLine(errore.ToString());
            }
            finally
            {
                // Aggiorna lo stato dei pulsanti
                btnConnect.IsEnabled = !connesso;
                btnDisconnect.IsEnabled = connesso;

                // se il server è disconesso ripristina i colori originali
                if ( !connesso )
                    lblTitolo.BackgroundColor = oldColor;
            }
        }

        async void btnConnect_Clicked(object sender, System.EventArgs e)
        {
            await Connect( true );    
        }

        async void btnDisconnect_Clicked(object sender, System.EventArgs e)
        {
            await Connect( false );
        }

        async Task Connect(bool connect)
        {
            if (connect)
            {
                // Forza la connessione
                if (Adapter.AdapterCanBeEnabled && Adapter.CurrentState.IsDisabledOrDisabling())
                {
                    Debug.WriteLine("Attivo adattatore.");
                    await Adapter.EnableAdapter();
                }

                Debug.WriteLine("Tento la connessione.");
                var connection = await Adapter.FindAndConnectToDevice(
                    new ScanFilter()
                        .SetAdvertisedDeviceName("Sensore Techno Back Brace")
                        //.SetAdvertisedManufacturerCompanyId(0xffff)
                        //.AddAdvertisedService(guid)
                        ,
                    TimeSpan.FromSeconds(30)
                );

                if (connection.IsSuccessful())
                {
                    Debug.WriteLine($"Connesso a {connection.GattServer.DeviceId} {connection.GattServer.Advertisement.DeviceName}");
                    GattServer = connection.GattServer;

                    try
                    {
                        notifyHandler = GattServer.NotifyCharacteristicValue(
                           serviceGuid,
                           buttonGuid,
                           bytes => {
                               // Attento. Qui può arrivarti un solo byte o due o quattro a seconda del tipo
                               // di dato che hai devinito lato ESP32...
                               // Ora lato ESP32 ho usato un uint16_t
                               var val = BitConverter.ToUInt16(bytes, 0);
                               Debug.WriteLine($"{bytes.Count()} byte ({val}) da {buttonGuid}");
                               if (val == 1)
                                   lblTitolo.BackgroundColor = Color.Red;
                               else
                                   lblTitolo.BackgroundColor = oldColor;

                           }
                        );
                    }
                    catch (GattException ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }


                    /*
                    foreach(var sGuid in await GattServer.ListAllServices())
                    {
                        Debug.WriteLine($"service: {known.GetDescriptionOrGuid(sGuid)}");

                        foreach (var cGuid in await GattServer.ListServiceCharacteristics(sGuid))
                        {
                            Debug.WriteLine($"characteristic: {known.GetDescriptionOrGuid(cGuid)}");

                            // Memorizza i Guid
                            serviceGuid = sGuid;
                            charGuid = cGuid;

                            try
                            {
                                var valueArray = await GattServer.ReadCharacteristicValue(sGuid, cGuid);
                                var intValue = BitConverter.ToInt32(valueArray, 0);
                                Debug.WriteLine(intValue);

                                try
                                {
                                    // Will also stop listening when gattServer
                                    // is disconnected, so if that is acceptable,
                                    // you don't need to store this disposable.
                                    notifyHandler = GattServer.NotifyCharacteristicValue(
                                       sGuid,
                                       cGuid,
                                       // IObserver<Tuple<Guid, Byte[]>> or IObserver<Byte[]> or
                                       // Action<Tuple<Guid, Byte[]>> or Action<Byte[]>
                                       bytes => {
                                           var intValue2 = BitConverter.ToInt32(bytes, 0);
                                           Debug.WriteLine(intValue2);
                                           Pulsante = intValue2 == 1 ? true : false;

                                           if (Pulsante)
                                               lblTitolo.BackgroundColor = Color.Red;
                                           else
                                               lblTitolo.BackgroundColor = oldColor;

                                       });
                                }
                                catch (GattException ex)
                                {
                                    Debug.WriteLine(ex.ToString());
                                }

                            }
                            catch (GattException ex)
                            {
                                Debug.WriteLine(ex.ToString());
                            }
                        }
                    }
                    */
                }
                else
                    Debug.WriteLine("Error connecting to device. result={0:g}", connection.ConnectionResult);
            }
            else
            {
                // Forza la disconnessione
                if (GattServer != null)
                {
                    if (GattServer.State == ConnectionState.Connected ||
                        GattServer.State == ConnectionState.Connecting)
                    {
                        await GattServer.Disconnect();

                        // una volta disconnesso, meglio spegnere anche i notificatori...
                        notifyHandler.Dispose();
                    }
                }
            }

            Debug.WriteLine($"Stato della connessione: {GattServer.State}");
        }
    }
}
