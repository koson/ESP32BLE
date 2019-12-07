/*
ESP32BLE

Esempio di utilizzo di ESP32 per una connessione BLE.

Realizzato in Maggio 2019 da Maurizio Conti
maurizio.conti@fablabromagna.org

Licenza GPLv3

Testato su scheda WeMos D1 R32 con scheda Grove 
- led BLU -> D5
- led ROSSO -> D6
- pulsante -> D7


Pinout di ESP32
https://docs.google.com/document/d/1oocFyBbZyG31h97RjGwavDIS8yAIoPVqfHgOXjkzUbk/edit
*/


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

        IDisposable notifyHandler;

        Color oldColor;

        //public ICommand cmdConnect { get; }
        //public ICommand cmdDisconnect { get; }

        public MainPage(IBluetoothLowEnergyAdapter adapter)
        {
            Adapter = adapter;

            // i command così non si bindano... Devono essere messi all'interno di una INotifyPropertyChanged...
            //cmdConnect = new Command(async () => await Connect(true));
            //cmdDisconnect = new Command(async () => await Connect(false));

            InitializeComponent();
            oldColor = lblTitolo.BackgroundColor;

        }


        async void btnConnect_Clicked(object sender, System.EventArgs e)
        {
            try
            {
                btnConnect.IsEnabled = false;

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
                    TimeSpan.FromSeconds(10)
                );


                if (connection.IsSuccessful())
                {
                    await Navigation.PushModalAsync(new BLEPage(Adapter, connection));
                }
                else
                {
                    await DisplayAlert("Errore", "Device non trovato...", "OK");
                }
                btnConnect.IsEnabled = true;
            }
            catch (Exception errore)
            {
                await DisplayAlert("Errore", errore.Message, "OK");
            }
        }

        void Handle_Appearing(object sender, System.EventArgs e)
        {
            btnConnect.IsEnabled = true;
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
                           bytes =>
                           {
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
