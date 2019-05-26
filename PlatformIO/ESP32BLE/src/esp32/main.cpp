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


#include <Arduino.h>

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLE2902.h>

void Debounce( void );

// Define per mappare i connettori Grove su ESP8266
#define ESP_D2 16
#define ESP_D3 5
#define ESP_D4 4
#define ESP_D5 0
#define ESP_D6 2
#define ESP_BUILTIN ESP_D6
#define ESP_D7 14
#define ESP_D8 12
#define ESP_D9 13

// Define per mappare i connettori Grove su ESP32
#define ESP32_D2 26
#define ESP32_D3 25
#define ESP32_D4 17
#define ESP32_D5 16
#define ESP32_D6 27
#define ESP32_BUILTIN 2
#define ESP32_D7 14
#define ESP32_D8 12
#define ESP32_D9 13

// Mapping del led e del pulsante
#define LED_ROSSO ESP32_D6
#define LED_BLU ESP32_D5
#define PULSANTE1 ESP32_D7

// tempo oltre il quale il pulsante lo consideriamo stabile
#define DEBOUCE_TIME 10

// velocità di update delle notifiche BLE
#define BLE_NOTIFY_UPDATE_TIME 100


bool deviceConnected = false;
int stato = 0;

unsigned long prossimoTick = 0;
unsigned long lastDebounceTime = 0;

uint8_t localSliderValue = 0;
uint8_t localOldSliderValue = 0;

uint16_t localButtonValue = 0;
uint16_t localOldButtonValue = 0;

// Variables will change:
int ledState = HIGH;         // the current state of the output pin
int buttonState;             // the current reading from the input pin
int lastButtonState = LOW;   // the previous reading from the input pin

// UUID BLE utilizzati
#define SERVICE_UUID "ABCD1234-0aaa-467a-9538-01f0652c74e8"
#define SLIDER_VALUE_UUID "ABCD1235-0aaa-467a-9538-01f0652c74e8"
#define BUTTON_VALUE_UUID "ABCD1236-0aaa-467a-9538-01f0652c74e8"

// Handler delle Caratteristiche utilizzate
BLECharacteristic *sliderCharacteristic = NULL;
BLECharacteristic *buttonCharacteristic = NULL;

// Gestione eventi server BLE
class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      Serial.println("BLE Client Connected");
      deviceConnected = true;
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );
      localButtonValue = ledState = localOldButtonValue = LOW;
    };

    void onDisconnect(BLEServer* pServer) {
      Serial.println("BLE Client Disconnected");
      deviceConnected = false;
      digitalWrite( LED_BLU, LOW );
    }
};

// Gestione eventi BLE dello slider
class SliderValueCallbacks: public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic *pCharacteristic) {
      Serial.print("Write slider value ");
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );

      // dati grezzi come puntatore di byte
      uint8_t* v = pCharacteristic->getData();
      localSliderValue = v[0];

      // Libreria per aggiungere analogWrite a ESP32
      // https://github.com/ERROPiX/ESP32_AnalogWrite
      long lux = map( localSliderValue, 0, 100, 0, 255);
      ledcWrite(0, lux);

      Serial.print( localSliderValue );
      Serial.println( "." );      
    }
};

// Gestione eventi BLE del pulsante
class ButtonValueCallbacks: public BLECharacteristicCallbacks {
    void onRead(BLECharacteristic *pCharacteristic) {
      Serial.println("Read button value");
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );
    }
};

void setup() {

  // Usiamo tre led di cui uno PWM e un pulsante 
  pinMode( ESP32_BUILTIN, OUTPUT );
  pinMode( LED_ROSSO, OUTPUT );

  pinMode( PULSANTE1, PULLUP );
  
  // In ESP32 analogWrite non c'è
  // Ci sono 16 canali PWM e vanno inizializzati
  // Qui inizializzo a 5KHz/8bit il canale 0
  #if defined(ARDUINO_ARCH_ESP32)
		ledcAttachPin(LED_BLU, 0);
  	ledcSetup(0, 5000, 8);
	#else
		pinMode(LED_BLU, OUTPUT);
	#endif

  Serial.begin(115200);
  Serial.println("\n\nESP32 Startup");

  // BLEDevice racchiude le funzionalita BLE di ESP32 (Kolbam).
  BLEDevice::init( "Sensore Techno Back Brace" );
  Serial.println("Sensore TBB acceso!");

  // Con BLEDevice possiamo ora tirare su un BLE Server con relativo callback
  BLEServer *pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  // All'interno del server BLE possiamo esporre servizi.
  // Un servizio è un container con N characteristics.
  BLEService* pService = pServer->createService(SERVICE_UUID);
 
  // Quante characteristics ci sono in un service lo decidiamo noi.
  // Per ogni dato da scambiare con la App Mobile, possiamo
  // creare una characteristic.
  // 
  // Ogni characteristic può essere vista come un registro remoto.
  // Scrivere/leggere sulla/dalla char è come scrivere/leggere 
  // in/da una variabile che sta sulla App Mobile
  //
  // Questa characteristic può essere letta e/o scritta da remoto 
  // ma può anche forzare una notifica che scatena un evento
  // sull'altro endpoint (molto efficiente!!) 
  sliderCharacteristic = pService->createCharacteristic( 
      SLIDER_VALUE_UUID, 
      BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_WRITE);
  // Attacco l'event handler
  sliderCharacteristic->setCallbacks(new SliderValueCallbacks());
  // Non ho ben capito se serva un descrittore... funziona anche senza!
  //sliderCharacteristic->addDescriptor(new BLE2902());

  buttonCharacteristic = pService->createCharacteristic( 
      BUTTON_VALUE_UUID, 
      BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_READ);
  // Attacco l'event handler
  buttonCharacteristic->setCallbacks(new ButtonValueCallbacks());


  // Si parte!
  pService->start();

  // Ci si fa vedere in giro... 
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->start();

  Serial.println( "Partiti..." );
}


void loop() {

  // Acquisizione pulsante
  Debounce();

  // Tick!
  if ( deviceConnected ) {
    
    // Le notifiche BLE ogni 100 mS.      
    if( millis() > prossimoTick ) {

      // Tempo scaduto.
      // Sono passati altri BLE_NOTIFY_UPDATE_TIME ms
      prossimoTick = millis() + BLE_NOTIFY_UPDATE_TIME;
    
      stato = !stato;
      digitalWrite( ESP32_BUILTIN, stato );

      // se guardo ledState ho un toggle...
      localButtonValue = ledState == LOW ? 0 : 1;

      // se guardo buttonState ho lo stato attuale
      //valore = buttonState == LOW ? 0 : 1;

      digitalWrite( LED_ROSSO, localButtonValue);

      if( localButtonValue != localOldButtonValue ){
        localOldButtonValue = localButtonValue;

        // aggiorniamo il valore della characteristic 
        buttonCharacteristic->setValue( localButtonValue );
      
        // possiamo notificare il cambiamento alla App mobile
        buttonCharacteristic->notify();

        Serial.print("Bottone: ");
        Serial.println(localButtonValue);
      }
    }

    /* Gestione della notifica  
    try
    {
        // Inizializzazione
        IDisposable notifyHandler = GattServer.NotifyCharacteristicValue(
            serviceGuid, charGuid,
            bytes => {
                var intValue2 = BitConverter.ToInt32(bytes, 0);
                Debug.WriteLine(intValue2);
            });
        
        // Alla fine ricordarsi di chiudere tutto...
        notifyHandler.Dispose();
    }
    catch (GattException ex) { Debug.WriteLine(ex.ToString()); }
    ****/

  }
  else
  {
    digitalWrite( LED_BLU, LOW );
    Serial.print("."); // keep alive
    delay(100);
  }    
}

//
// Metodo passante (non bloccante) per gestire l'antirimbalzo del pulsante. 
//
// dal main...
// - guardando ledState si ha un toggle per ogni pressione
// - guardando buttonState si ha lo stato del pulsante
//
void Debounce()
{
  //https://www.arduino.cc/en/Tutorial/Debounce
  
  int reading = digitalRead(PULSANTE1);
  if (reading != lastButtonState) {
    lastDebounceTime = millis();
  }

  if ((millis() - lastDebounceTime) > DEBOUCE_TIME) {
    if (reading != buttonState) {
      buttonState = reading;

      if (buttonState == HIGH) {
        ledState = !ledState;
      }
    }
  }

  lastButtonState = reading;
}