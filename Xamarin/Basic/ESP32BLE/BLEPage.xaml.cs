
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using nexus.protocols.ble;
using nexus.protocols.ble.gatt;
using nexus.protocols.ble.scan;
using Xamarin.Forms;

namespace ESP32BLE
{
    public partial class BLEPage : ContentPage
    {
        IBluetoothLowEnergyAdapter Adapter { get; set; }
        IBleGattServerConnection GattServer { get; set; }
        BlePeripheralConnectionRequest Connection { get; set; }

        Guid serviceGuid = new Guid("ABCD1234-0aaa-467a-9538-01f0652c74e8");
        Guid sliderGuid = new Guid("ABCD1235-0aaa-467a-9538-01f0652c74e8");
        Guid buttonGuid = new Guid("ABCD1236-0aaa-467a-9538-01f0652c74e8");
        Guid adcGuid = new Guid("ABCD1237-0aaa-467a-9538-01f0652c74e8");

        IDisposable buttonNotifyHandler;
        IDisposable adcNotifyHandler;

        Color oldColor;
        int oldSliderValue = 1;
        int indicatoreConnessione = 0;

        //public ICommand cmdConnect { get; }
        //public ICommand cmdDisconnect { get; }

        public ESP32BLE.Models.AdcValues Valori { get; set; }

        public BLEPage() { }
        public BLEPage(IBluetoothLowEnergyAdapter adapter, BlePeripheralConnectionRequest connection)
        {
            Adapter = adapter;
            Connection = connection;

            // i command così non si bindano... Devono essere messi all'interno di una INotifyPropertyChanged...
            //cmdConnect = new Command(async () => await Connect(true));
            //cmdDisconnect = new Command(async () => await Connect(false));

            InitializeComponent();
            oldColor = lblTitolo.BackgroundColor;

            Valori = new Models.AdcValues();

            Debug.WriteLine($"Connesso a {connection.GattServer.DeviceId} {connection.GattServer.Advertisement.DeviceName}");
            GattServer = connection.GattServer;

            try
            {
                // Mi registro per ricevere le notifiche del bottone lato ESP32
                buttonNotifyHandler = GattServer.NotifyCharacteristicValue(
                   serviceGuid,
                   buttonGuid,
                   bytes =>
                   {
                       // Attento. Qui può arrivarti un solo byte o due o quattro a seconda del tipo
                       // di dato che hai definito lato ESP32...
                       // Ora lato ESP32 ho usato un uint16_t
                       var valuleFromESP32 = BitConverter.ToUInt16(bytes, 0);
                       Debug.WriteLine($"{bytes.Count()} byte ({valuleFromESP32}) da {buttonGuid}");

                       swESP32.IsToggled = valuleFromESP32 == 1;

                   }
                );

                // Mi registro per ricevere le notifiche dell ADC lato ESP32
                adcNotifyHandler = GattServer.NotifyCharacteristicValue(
                   serviceGuid,
                   adcGuid,
                   bytes =>
                   {
                       // Attento. Qui può arrivarti un solo byte o due o quattro a seconda del tipo
                       // di dato che hai definito lato ESP32...
                       // Ora lato ESP32 ho usato un int (signed)
                       var valuleFromESP32 = BitConverter.ToInt32(bytes, 0);
                       Debug.WriteLine($"{bytes.Count()} byte ({valuleFromESP32}) da {adcGuid}");

                       lblADCVal.Text = valuleFromESP32.ToString();
                       Valori.Add(new Models.AdcValue { Time = DateTime.Now, Value = valuleFromESP32 });
                       if (Valori.Count > 100)
                           Valori.RemoveAt(0);

                       chart.ItemsSource = null;
                       chart.ItemsSource = Valori;

                   }
                );

                Device.StartTimer(TimeSpan.FromSeconds(0.2), () =>
                {
                    AggiornaStato();
                    return true; // True = Repeat again, False = Stop the timer
                });

            }
            catch (GattException ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }


        async void btnDisconnect_Clicked(object sender, System.EventArgs e)
        {
            // Forza la disconnessione
            if (GattServer != null)
            {
                if (GattServer.State == ConnectionState.Connected ||
                    GattServer.State == ConnectionState.Connecting)
                {
                    await GattServer.Disconnect();

                }
            }

            // una volta disconnesso, meglio spegnere anche i notificatori...
            if (buttonNotifyHandler != null)
                buttonNotifyHandler.Dispose();

            await Navigation.PopModalAsync();
        }

        async void AggiornaStato()
        {
            bool connesso = false;

            if (GattServer != null)
            {
                connesso = GattServer.State == ConnectionState.Connected;

                // Aggiorna visualizzazione stato connessione;
                GestioneStatoConnessione(connesso);

                if (connesso)
                {
                    try
                    {
                        int sliderValore = Convert.ToInt32(slValore.Value);

                        if (sliderValore != oldSliderValue)
                        {
                            oldSliderValue = sliderValore;

                            // Spedisce il valore dello slider
                            byte[] bufferDaSpedire = BitConverter.GetBytes(sliderValore);

                            var result = await GattServer.WriteCharacteristicValue(
                                serviceGuid, sliderGuid,
                                bufferDaSpedire
                            );
                        }
                    }
                    catch (Exception errore)
                    {
                        Debug.WriteLine(errore.ToString());
                    }
                }
            }
        }

        void GestioneStatoConnessione(bool connesso)
        {
            slValore.IsEnabled = connesso;

            if (connesso)
            {
                lblTitolo.Text = "ESP32 connesso";
                indicatoreConnessione++;
                if (indicatoreConnessione > 3)
                {
                    indicatoreConnessione = 0;
                    lblStatoConnesso.TextColor = lblStatoConnesso.TextColor != Color.Red ? Color.Red : Color.Black;
                }
            }
            else
            {
                swESP32.IsToggled = false;
                lblStatoConnesso.TextColor = Color.Black;
                lblTitolo.Text = "ESP32 disconnesso!";
            }
        }
    }
}
