//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Intent;

namespace Microsoft.CognitiveServices.Speech.Tests.EndToEnd
{
    using System.Threading;
    using static AssertHelpers;
    using static SpeechRecognitionTestsHelper;

    [TestClass]
    public class IntentRecognitionTests
    {
        private static string languageUnderstandingSubscriptionKey;
        private static string languageUnderstandingServiceRegion;
        private static string languageUnderstandingHomeAutomationAppId;
        private static string endpointInString;
        private static Uri endpointUrl;

        private SpeechConfig config;

        [ClassInitialize]
        public static void TestClassinitialize(TestContext context)
        {
            languageUnderstandingSubscriptionKey = Config.GetSettingByKey<String>(context, "LanguageUnderstandingSubscriptionKey");
            languageUnderstandingServiceRegion = Config.GetSettingByKey<String>(context, "LanguageUnderstandingServiceRegion");
            languageUnderstandingHomeAutomationAppId = Config.GetSettingByKey<String>(context, "LanguageUnderstandingHomeAutomationAppId");
            var intentRegionInUrl = MapToIntentServiceRegion(languageUnderstandingServiceRegion);
            endpointInString = String.Format("wss://speech.platform.bing.com/speech/{0}/recognition/interactive/cognitiveservices/v1", intentRegionInUrl);
            endpointUrl = new Uri(endpointInString);

            var inputDir = Config.GetSettingByKey<String>(context, "InputDir");
            TestData.AudioDir = Path.Combine(inputDir, "audio");
            TestData.KwsDir = Path.Combine(inputDir, "kws");
        }

        [TestInitialize]
        public void Initialize()
        {
            config = SpeechConfig.FromSubscription(languageUnderstandingSubscriptionKey, languageUnderstandingServiceRegion);
        }

        public static string MapToIntentServiceRegion(string speechServiceRegion)
        {
            var regionMap = new Dictionary<string, string>
            {
                {"westus", "uswest"},
                {"westus2", "uswest2"},
                {"southcentralus", "ussouthcentral"},
                {"westcentralus", "uswestcentral"},
                {"eastus", "useast"},
                {"eastus2", "useast2"},
                {"westeurope", "europewest"},
                {"northeurope", "europenorth"},
                {"southbrazil", "brazilsouth"},
                {"eastaustralia", "australiaeast"},
                {"southeastasia", "asiasoutheast"},
                {"eastasia", "asiaeast"}
            };

            Assert.IsTrue(regionMap.ContainsKey(speechServiceRegion), "Cannot map speech service region to intent service region.");
            return regionMap[speechServiceRegion];
        }

        public static IntentRecognizer TrackSessionId(IntentRecognizer recognizer)
        {
            recognizer.SessionStarted += (s, e) =>
            {
                Console.WriteLine("SessionId: " + e.SessionId);
            };

            recognizer.Canceled += (s, e) =>
            {
                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CancellationReason.Error: ErrorCode {e.ErrorCode}, ErrorDetails {e.ErrorDetails}");
                }
            };
            return recognizer;
        }

        [DataTestMethod]
        [DataRow("", "", "HomeAutomation.TurnOn")]
        [DataRow("", "my-custom-intent-id-string", "my-custom-intent-id-string")]
        [DataRow("HomeAutomation.TurnOn", "", "HomeAutomation.TurnOn")]
        [DataRow("HomeAutomation.TurnOn", "my-custom-intent-id-string", "my-custom-intent-id-string")]
        [DataRow("intent-name-that-doesnt-exist", "", "")]
        [DataRow("intent-name-that-doesnt-exist", "my-custom-intent-id-string", "")]
        public async Task RecognizeIntent(string intentName, string intentId, string expectedIntentId)
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var model = LanguageUnderstandingModel.FromAppId(languageUnderstandingHomeAutomationAppId);
                if (string.IsNullOrEmpty(intentName) && string.IsNullOrEmpty(intentId))
                {
                    recognizer.AddAllIntents(model);
                }
                else if (string.IsNullOrEmpty(intentName))
                {
                    recognizer.AddAllIntents(model, intentId);
                }
                else if (string.IsNullOrEmpty(intentId))
                {
                    recognizer.AddIntent(model, intentName);
                }
                else
                {
                    recognizer.AddIntent(model, intentName, intentId);
                }

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                Assert.AreEqual(
                    string.IsNullOrEmpty(expectedIntentId) ? ResultReason.RecognizedSpeech : ResultReason.RecognizedIntent,
                    result.Reason);
                Assert.AreEqual(expectedIntentId, result.IntentId);
                Assert.AreEqual(TestData.English.HomeAutomation.TurnOn.Utterance, result.Text);
                var json = result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult);
                Assert.IsFalse(string.IsNullOrEmpty(json), "Empty JSON from intent recognition");
                // TODO check JSON validity
            }
        }

        [TestMethod]
        public async Task RecognizeIntentSpecialCharacters()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.TStockValue.AudioFile);
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var model = LanguageUnderstandingModel.FromAppId(languageUnderstandingHomeAutomationAppId);
                recognizer.AddAllIntents(model);
                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                var json = result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult);
                Assert.IsTrue(json.Contains("AT&T"), $"Could not find AT&T in JSON response: {json}");
            }
        }

        [TestMethod]
        public void TestSetAndGetAuthTokenOnIntent()
        {
            var token = "x";
            var config = SpeechConfig.FromAuthorizationToken(token, "westus");
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.Weather.AudioFile);

            using (var recognizer = new IntentRecognizer(config, audioInput))
            {
                Assert.AreEqual(token, recognizer.AuthorizationToken);

                var newToken = "y";
                recognizer.AuthorizationToken = newToken;
                Assert.AreEqual(token, config.AuthorizationToken);
                Assert.AreEqual(newToken, recognizer.AuthorizationToken);
            }
        }

        [TestMethod]
        public async Task TestSetAuthorizationTokenOnIntentRecognizer()
        {
            var invalidToken = "InvalidToken";
            var configWithToken = SpeechConfig.FromAuthorizationToken(invalidToken, languageUnderstandingServiceRegion);
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.Weather.AudioFile);

            using (var recognizer = TrackSessionId(new IntentRecognizer(configWithToken, audioInput)))
            {
                Assert.AreEqual(invalidToken, recognizer.AuthorizationToken);

                var newToken = await Config.GetToken(languageUnderstandingSubscriptionKey, languageUnderstandingServiceRegion);
                recognizer.AuthorizationToken = newToken;

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                Assert.AreEqual(newToken, recognizer.AuthorizationToken);
                AssertMatching(TestData.English.Weather.Utterance, result.Text);
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task RecognizeIntentSimplePhrase(bool matchingPhrase, bool singleArgument)
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);

            var phrase = matchingPhrase ? TestData.English.HomeAutomation.TurnOn.Utterance : "do not match this";
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var someId = "id1";
                var expectedId = matchingPhrase ? (singleArgument ? phrase : someId) : "";
                if (singleArgument)
                {
                    recognizer.AddIntent(phrase);
                }
                else
                {
                    recognizer.AddIntent(phrase, someId);
                }
                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                // TODO cannot enable below assertion yet, RecognizedIntent is not returned - VSO:1594523
                //Assert.AreEqual(
                //    string.IsNullOrEmpty(expectedId) ? ResultReason.RecognizedSpeech : ResultReason.RecognizedIntent,
                //    result.Reason);
                Assert.AreEqual(TestData.English.HomeAutomation.TurnOn.Utterance, result.Text);
                Assert.AreEqual(expectedId, result.IntentId,
                    $"Unexpected intent ID for singleArgument={singleArgument} matchingPhrase={matchingPhrase}: is {result.IntentId}, expected {expectedId}");
            }
        }

        [TestMethod]
        public async Task IntentRecognizerConnectedEvent()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);
            int connectedEventCount = 0;
            int disconnectedEventCount = 0;
            EventHandler<ConnectionEventArgs> myConnectedHandler = (s, e) =>
            {
                connectedEventCount++;
            };
            EventHandler<ConnectionEventArgs> myDisconnectedHandler = (s, e) =>
            {
                disconnectedEventCount++;
            };
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var connection = Connection.FromRecognizer(recognizer);
                var model = LanguageUnderstandingModel.FromAppId(languageUnderstandingHomeAutomationAppId);
                recognizer.AddIntent(model, "HomeAutomation.TurnOn", "my-custom-intent-id-string");

                var tcs = new TaskCompletionSource<int>();
                recognizer.SessionStopped += (s, e) =>
                {
                    tcs.TrySetResult(0);
                };
                recognizer.Canceled += (s, e) =>
                {
                    Console.WriteLine("Canceled: " + e.SessionId);
                    tcs.TrySetResult(0);
                };
                connection.Connected += myConnectedHandler;
                connection.Disconnected += myDisconnectedHandler;

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(2)));

                connection.Connected -= myConnectedHandler;
                connection.Disconnected -= myDisconnectedHandler;

                Console.WriteLine($"ConnectedEventCount: {connectedEventCount}, DisconnectedEventCount: {disconnectedEventCount}");
                Assert.IsTrue(connectedEventCount > 0, AssertOutput.ConnectedEventCountMustNotBeZero);
                Assert.IsTrue(connectedEventCount == disconnectedEventCount || connectedEventCount == disconnectedEventCount + 1, AssertOutput.ConnectedDisconnectedEventUnmatch);

                Assert.AreEqual(ResultReason.RecognizedIntent, result.Reason);
            }
        }

        [TestMethod]
        public void IntentRecognizerUsingConnectionOpen()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var connection = Connection.FromRecognizer(recognizer);
                var ex = Assert.ThrowsException<ApplicationException>(() => connection.Open(false));
                AssertStringContains(ex.Message, "Exception with an error code: 0x1f");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public async Task AsyncRecognitionAfterDisposingIntentRecognizer()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);
            var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput));
            recognizer.AddIntent(TestData.English.HomeAutomation.TurnOn.Utterance);
            recognizer.Dispose();
            await recognizer.StartContinuousRecognitionAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DisposingIntentRecognizerWhileAsyncRecognition()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile);
            var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput));
            recognizer.AddIntent(TestData.English.HomeAutomation.TurnOn.Utterance);
            recognizer = DoAsyncRecognitionNotAwaited(recognizer);
        }

        [TestMethod]
        public async Task FromEndpointIntentModel()
        {
            var configFromEndpoint = SpeechConfig.FromEndpoint(endpointUrl, languageUnderstandingSubscriptionKey);
            configFromEndpoint.SpeechRecognitionLanguage = Language.EN;
            using (var recognizer = TrackSessionId(new IntentRecognizer(configFromEndpoint, AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile))))
            {
                var phrase = TestData.English.HomeAutomation.TurnOn.Utterance;
                var model = LanguageUnderstandingModel.FromAppId(languageUnderstandingHomeAutomationAppId);
                recognizer.AddAllIntents(model);

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                Assert.AreEqual(ResultReason.RecognizedIntent, result.Reason);
                Assert.AreEqual(phrase, result.Text);
                Assert.AreEqual("HomeAutomation.TurnOn", result.IntentId, $"Unexpected intent ID: actual: {result.IntentId}, expected {phrase}");

                var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                Assert.IsFalse(string.IsNullOrEmpty(json), "Empty JSON from intent recognition");
                Assert.IsTrue(json.Contains("ITN"), "Detailed result does not contain ITN.");
                Assert.IsTrue(json.Contains("Lexical"), "Detailed result does not contain Lexical.");
                Assert.IsTrue(json.Contains("MaskedITN"), "Detailed result does not contain MaskedITN.");
                Assert.IsTrue(json.Contains("Display"), "Detailed result does not contain Text.");
            }
        }

        [TestMethod]
        public async Task FromEndpointGermanPhraseIntent()
        {
            var configFromEndpoint = SpeechConfig.FromEndpoint(endpointUrl, languageUnderstandingSubscriptionKey);
            configFromEndpoint.SpeechRecognitionLanguage = Language.DE_DE;
            // Although we set output format to simple, the intent recognizer will set it to detailed for checking intents.
            configFromEndpoint.OutputFormat = OutputFormat.Simple;
            using (var recognizer = TrackSessionId(new IntentRecognizer(configFromEndpoint, AudioConfig.FromWavFileInput(TestData.German.FirstOne.AudioFile))))
            {
                var phrase = TestData.German.FirstOne.Utterance;
                recognizer.AddIntent(phrase);

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                // TODO cannot enable below assertion yet, RecognizedIntent is not returned - VSO:1594523
                // Assert.AreEqual(ResultReason.RecognizedIntent, result.Reason);
                Assert.AreEqual(ResultReason.RecognizedSpeech, result.Reason);
                Assert.AreEqual(phrase, result.Text);
                Assert.AreEqual(phrase, result.IntentId, $"Unexpected intent ID: actual: {result.IntentId}, expected {phrase}");

                var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                Assert.IsFalse(string.IsNullOrEmpty(json), "Empty JSON from intent recognition");
                Assert.IsTrue(json.Contains("ITN"), "Detailed result does not contain ITN.");
                Assert.IsTrue(json.Contains("Lexical"), "Detailed result does not contain Lexical.");
                Assert.IsTrue(json.Contains("MaskedITN"), "Detailed result does not contain MaskedITN.");
                Assert.IsTrue(json.Contains("Display"), "Detailed result does not contain Text.");
            }
        }

        [TestMethod]
        public async Task FromEndpointPropertyOverwriteIntent()
        {
            var endpointWithProperty = endpointInString + "?language=en-us&format=detailed";
            var configFromEndpoint = SpeechConfig.FromEndpoint(new Uri(endpointWithProperty), languageUnderstandingSubscriptionKey);

            // The property should not overwrite the query parameter in url.
            configFromEndpoint.SpeechRecognitionLanguage = "Invalid-Language";
            configFromEndpoint.OutputFormat = OutputFormat.Simple;

            using (var recognizer = TrackSessionId(new IntentRecognizer(configFromEndpoint, AudioConfig.FromWavFileInput(TestData.English.HomeAutomation.TurnOn.AudioFile))))
            {
                var phrase = TestData.English.HomeAutomation.TurnOn.Utterance;
                var model = LanguageUnderstandingModel.FromAppId(languageUnderstandingHomeAutomationAppId);
                recognizer.AddAllIntents(model);

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

                Assert.AreEqual(ResultReason.RecognizedIntent, result.Reason);
                Assert.AreEqual(phrase, result.Text);
                Assert.AreEqual("HomeAutomation.TurnOn", result.IntentId, $"Unexpected intent ID: actual: {result.IntentId}, expected {phrase}");

                var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                Assert.IsFalse(string.IsNullOrEmpty(json), "Empty JSON from intent recognition");
                Assert.IsTrue(json.Contains("ITN"), "Detailed result does not contain ITN.");
                Assert.IsTrue(json.Contains("Lexical"), "Detailed result does not contain Lexical.");
                Assert.IsTrue(json.Contains("MaskedITN"), "Detailed result does not contain MaskedITN.");
                Assert.IsTrue(json.Contains("Display"), "Detailed result does not contain Text.");
            }
        }

        [TestMethod]
        public async Task RecognizeIntentDefaultLanguage()
        {
            var audioInput = AudioConfig.FromWavFileInput(TestData.English.TStockValue.AudioFile);
            using (var recognizer = TrackSessionId(new IntentRecognizer(config, audioInput)))
            {
                var recoLanguage = recognizer.Properties.GetProperty(PropertyId.SpeechServiceConnection_RecoLanguage);
                Assert.IsTrue(String.IsNullOrEmpty(recoLanguage), "RecoLanguage should not be set here. RecoLanguage: " + recoLanguage);

                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                var connectionUrl = recognizer.Properties.GetProperty(PropertyId.SpeechServiceConnection_Url);
                Assert.IsTrue(connectionUrl.Contains("language=en-us"), "Incorrect default language (should be en-us) in " + connectionUrl);
            }
        }

        IntentRecognizer DoAsyncRecognitionNotAwaited(IntentRecognizer rec)
        {
            using (var recognizer = rec)
            {
                recognizer.RecognizeOnceAsync();
                Thread.Sleep(100);
                return recognizer;
            }
        }
    }
}
