using System;
using nexus.protocols.ble;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ESP32BLE
{
    public partial class App : Application
    {
        public App(IBluetoothLowEnergyAdapter adapter)
        {
            // Register Syncfusion license
            // https://help.syncfusion.com/common/essential-studio/licensing/license-key?_ga=2.67405393.1164859487.1560159234-903112670.1553948214#how-to-generate-syncfusion-license-key
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MTA5MzEwQDMxMzcyZTMxMmUzMEowZWF2bWFEMktJdSt6WlM3dGVXSVhKeHk3YVUxRjFHa2JXRjlWS3hMZTg9");

            InitializeComponent();
            MainPage = new NavigationPage( new MainPage( adapter ) );
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
