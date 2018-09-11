# Speech Sample

This sample demostrates how to build a simple speech recognition application using a number of different \psi audio and speech components. In addition, it also demonstrates visualization of live data, data logging, and replay of logged data.

NOTES: 
- In order to run the BingSpeechRecognizer portion of this sample (option 2 in the sample menu), you must have a valid Cognitive Services Bing Speech API subscription key. You may enter this key at runtime, or set it in the static `BingSubscriptionKey` variable. For more information on how to obtain a subscription key for the Bing Speech API, see:
  - https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-apis-create-account.
- In order to run the MicrosoftSpeechRecognizer portion of the sample (option 3), you must have separately installed the Microsoft Speech Platform runtime and language pack. For more details, see:
  - https://www.microsoft.com/en-us/download/details.aspx?id=27225
  - https://www.microsoft.com/en-us/download/details.aspx?id=27224
- When disk logging (option 5) is turned on, audio and speech recognition stream data will be logged to folders on your hard drive. By default, the root location where log folders will be created is the relative path `..\..\..\Data\PsiSpeechSample` (relative to the working directory of the running sample). You may change this by setting the static `LogPath` variable in the sample code.
- Playback from a logged session (option 4) requires that at least one logged session be previously saved to disk (option 5).
- Live visualization (option 6) requires that Psi Studio be installed first.