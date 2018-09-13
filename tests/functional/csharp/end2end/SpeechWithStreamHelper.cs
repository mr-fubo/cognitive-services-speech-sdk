//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//
using MicrosoftSpeechSDKSamples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace Microsoft.CognitiveServices.Speech.Tests.EndToEnd
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static SpeechRecognitionTestsHelper;

    sealed class SpeechWithStreamHelper
    {
        private readonly TimeSpan timeout;

        public SpeechWithStreamHelper()
        {
            timeout = TimeSpan.FromMinutes(6);
        }

        SpeechRecognizer CreateSpeechRecognizerWithStream(SpeechConfig config, string audioFile)
        {
            var audioInput = Util.OpenWavFile(audioFile);
            return new SpeechRecognizer(config, audioInput);
        }

        public async Task<SpeechRecognitionResult> GetSpeechFinalRecognitionResult(SpeechConfig config, string audioFile)
        {
            using (var recognizer = TrackSessionId(new SpeechRecognizer(config, AudioConfig.FromWavFileInput(audioFile))))
            {
                SpeechRecognitionResult result = null;
                await Task.WhenAny(recognizer.RecognizeAsync().ContinueWith(t => result = t.Result), Task.Delay(timeout));
                return result;
            }
        }

        public async Task<List<SpeechRecognitionResultEventArgs>> GetSpeechFinalRecognitionContinuous(SpeechConfig config, string audioFile)
        {
            using (var recognizer = TrackSessionId(CreateSpeechRecognizerWithStream(config, audioFile)))
            {
                var tcs = new TaskCompletionSource<bool>();
                var textResultEvents = new List<SpeechRecognitionResultEventArgs>();

                recognizer.FinalResultReceived += (s, e) =>
                {
                    Console.WriteLine($"Received result {e.Result.ToString()}");
                    textResultEvents.Add(e);
                };

                recognizer.OnSessionEvent += (s, e) =>
                {
                    if (e.EventType == SessionEventType.SessionStoppedEvent)
                    {
                        tcs.TrySetResult(true);
                    }
                };
                string error = string.Empty;
                recognizer.RecognitionErrorRaised += (s, e) => { error = e.ToString(); };

                await recognizer.StartContinuousRecognitionAsync();
                await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                await recognizer.StopContinuousRecognitionAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    Assert.Fail($"Error received: {error}");
                }

                return textResultEvents;
            }
        }

        public async Task<List<List<SpeechRecognitionResultEventArgs>>> GetSpeechIntermediateRecognitionContinuous(SpeechConfig config, string audioFile)
        {
            using (var recognizer = TrackSessionId(CreateSpeechRecognizerWithStream(config, audioFile)))
            {
                var tcs = new TaskCompletionSource<bool>();
                var listOfIntermediateResults = new List<List<SpeechRecognitionResultEventArgs>>();
                List<SpeechRecognitionResultEventArgs> receivedIntermediateResultEvents = null;

                recognizer.OnSessionEvent += (s, e) =>
                {
                    if (e.EventType == SessionEventType.SessionStartedEvent)
                    {
                        receivedIntermediateResultEvents = new List<SpeechRecognitionResultEventArgs>();
                    }
                    if (e.EventType == SessionEventType.SessionStoppedEvent)
                    {
                        tcs.TrySetResult(true);
                    }
                };
                recognizer.IntermediateResultReceived += (s, e) => receivedIntermediateResultEvents.Add(e);
                recognizer.FinalResultReceived += (s, e) =>
                {
                    listOfIntermediateResults.Add(receivedIntermediateResultEvents);
                    receivedIntermediateResultEvents = new List<SpeechRecognitionResultEventArgs>();
                };
                string error = string.Empty;
                recognizer.RecognitionErrorRaised += (s, e) => { error = e.ToString(); };

                await recognizer.StartContinuousRecognitionAsync();
                await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                await recognizer.StopContinuousRecognitionAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    Assert.Fail($"Error received: {error}");
                }

                return listOfIntermediateResults;
            }
        }
    }
}
