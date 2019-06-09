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

// Variabili locali per il metodo Debounce...
int ledState = HIGH;
int buttonState;   
int lastButtonState = LOW;

// UUID BLE inventati da me...

// Uno mi serve per esporre il service
#define SERVICE_UUID "ABCD1234-0aaa-467a-9538-01f0652c74e8"

// All'interno del service ho due valori che scambio tra ESP32 e cellulare
// Ognuno di questi valori (characteristic per BLE...) ha un suo UUID (sempre inventato...)
#define SLIDER_VALUE_UUID "ABCD1235-0aaa-467a-9538-01f0652c74e8"
#define BUTTON_VALUE_UUID "ABCD1236-0aaa-467a-9538-01f0652c74e8"

// Handler dei due valori scambiati
BLECharacteristic *sliderCharacteristic = NULL;
BLECharacteristic *buttonCharacteristic = NULL;

// Gestione eventi server BLE
class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {

      // Quando lo smartphone si collega...

      // muovo la variabile locale che mi indica questo stato
      deviceConnected = true;

      // lo segnalo sulla seriale...
      Serial.println("BLE Client Connected");
      
      // lo segnalo con un blink sul led blu a bordo di ESP32
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );

      // Inizializzo lo stato del pulsante a spento
      localButtonValue = ledState = localOldButtonValue = LOW;
    };

    void onDisconnect(BLEServer* pServer) {

      // Quando lo smartphone si scollega...

      // muovo la variabile locale che mi indica questo stato
      deviceConnected = false;

      // lo segnalo sulla seriale...
      Serial.println("BLE Client Disconnected");

    }
};

// Le characteristic BLE sono come registri remoti con una gestione a eventi.
// Per gestire questi eventi utilizziamo un CallBack lato esp32.
// La notifica parte lato smartphone muovendo uno slider.
// Il valore di questo slider arriva a esp32 il quale varia la luminosità di un led
class SliderValueCallbacks: public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic *pCharacteristic) {
      
      // Quando lato smartphone parte una notifica, ci troviamo qui...

      // Segnalo questo movimento di dati con un blink
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );

      // Leggo quindi il nuovo valore che mi arriva dallo smartphone 
      // e lo conservo in una variabile locale
      uint8_t* v = pCharacteristic->getData();
      localSliderValue = v[0];

      // Segnalo poi sulla seriale (debug)
      Serial.print("New slider value ");
      Serial.print( localSliderValue );
      Serial.println( "." );      
      
      // Attivo il PWM sul led che cambierà di intensità a seconda della
      // posizione dello slider sullo smartphone
      long lux = map( localSliderValue, 0, 100, 0, 255);
      ledcWrite(0, lux);      
    }
};

/*
// Questa characteristic invece viene usata per notificare allo smartphone
// la pressione del pulsante montato lato esp32.
class ButtonValueCallbacks: public BLECharacteristicCallbacks {
    void onRead(BLECharacteristic *pCharacteristic) {
      Serial.println("Read button value");
      digitalWrite( LED_BLU, HIGH );
      delay(10);
      digitalWrite( LED_BLU, LOW );
    }
};
*/

void setup() {

  // Usiamo tre led (di cui uno PWM) 
  pinMode( ESP32_BUILTIN, OUTPUT );
  pinMode( LED_ROSSO, OUTPUT );

  // e un pulsante
  pinMode( PULSANTE1, PULLUP );
  
  // Nota:
  // In ESP32 analogWrite non c'è
  // Esiste una lib  per aggiungere analogWrite a ESP32
  // https://github.com/ERROPiX/ESP32_AnalogWrite
  
  // Io qui uso le API di esp32
  // In esp32 ci sono 16 canali PWM.
  // Prima di usarli vanno inizializzati.
  
  // Inizializzo a 5KHz/8bit il canale 0
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

  // Grazie a BLEDevice Creaiamo un server BLE con relativo callback
  BLEServer *pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  // Un server BLE espone servizi.
  // Un servizio a sua volta è un container con N characteristics.
  // Esponiamo lo UUID del service
  BLEService* pService = pServer->createService(SERVICE_UUID);
 
  // Nel nostro service ci aggiungiamo due characteristic:
  // - una per ricevere il valore dello slider dallo smartphone
  // - una per comunicare allo smartphone lo stato del nostro pulsante esp32
  // 
  // Questa characteristic è di WRITE perchè può essere scritta da remoto 
  // ed è NOTIFY perchè può scatenare un evento lato esp32
  sliderCharacteristic = pService->createCharacteristic( 
      SLIDER_VALUE_UUID, 
      BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_WRITE);
  
  // Attacco l'event handler per gestire le notifiche
  sliderCharacteristic->setCallbacks(new SliderValueCallbacks());

  // Nota:  
  // Non ho ben capito come usare un descrittore.
  // funziona anche senza ma se serve ecco come si usa...
  //sliderCharacteristic->addDescriptor(new BLE2902());

  // Questa characteristic è di READ perchè può essere letta da remoto 
  buttonCharacteristic = pService->createCharacteristic( 
      BUTTON_VALUE_UUID, 
      BLECharacteristic::PROPERTY_READ);

  // Si parte!
  pService->start();

  // Facciamo un po' di pubblicità al nostro SERVICE.
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->start();

  Serial.println( "Partiti..." );
}


void loop() {

  // Acquisizione pulsante e gestione del debounce.
  // Leggendo ledState ho un toggle
  // Leggendo buttonState ho lo stato del pulsante
  Debounce();

  // Tick!
  if ( deviceConnected ) {
    
    // Le notifiche BLE ogni 100 mS (BLE_NOTIFY_UPDATE_TIME)  
    if( millis() > prossimoTick ) {

      // Tempo scaduto, sono passati altri BLE_NOTIFY_UPDATE_TIME ms
      // Ricarichiamo il timer della stufa...
      prossimoTick = millis() + BLE_NOTIFY_UPDATE_TIME;
    
      // Il led dell'ESP lo faccio blinkare per segnalare che siamo connessi...
      stato = !stato;
      digitalWrite( ESP32_BUILTIN, stato );

      // se guardo ledState ho un toggle...
      localButtonValue = ledState == LOW ? 0 : 1;

      // se guardo buttonState ho lo stato attuale
      //localButtonValue = buttonState == LOW ? 0 : 1;

      digitalWrite( LED_ROSSO, localButtonValue);

      if( localButtonValue != localOldButtonValue ){
        localOldButtonValue = localButtonValue;

        // aggiorniamo il valore della characteristic 
        buttonCharacteristic->setValue( localButtonValue );
      
        // questa notifica scatena la callback lato App mobile
        buttonCharacteristic->notify();

        Serial.print("Bottone: ");
        Serial.println(localButtonValue);
      }
    }
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