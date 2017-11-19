//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//
// uspclientconsole.c: A console application for testing USP client library.
//

#ifdef _MSC_VER
#define _CRT_SECURE_NO_WARNINGS
#endif

#include <stdio.h>
#include <Windows.h>
#include "usp.h"


void OnSpeechStartDetected(UspHandle handle, void* context, UspMsgSpeechStartDetected *message)
{
    printf("Speech.StartDetected message.\n");
}

void OnSpeechEndDetected(UspHandle handle, void* context, UspMsgSpeechEndDetected *message)
{
    printf("Speech.EndDetected message.\n");
}

void OnSpeechHypothesis(UspHandle handle, void* context, UspMsgSpeechHypothesis *message)
{
    printf("Speech.Hypothesis message. Text: %S\n", message->Text);
}

void OnSpeechPhrase(UspHandle handle, void* context, UspMsgSpeechPhrase *message)
{
    printf("Speech.Phrase message. Text: %S\n", message->DisplayText);
}

void OnTurnStart(UspHandle handle, void* context, UspMsgTurnStart *message)
{
    printf("Turn.Start message.\n");
}

void OnTurnEnd(UspHandle handle, void* context, UspMsgTurnEnd *message)
{
    printf("Turn.End message.\n");
}

void OnError(UspHandle handle, void* context, UspResult error)
{
    printf("On Error: %x.\n", error);
}

#define MAX_AUDIO_SIZE_IN_BYTE (256*1024)

int main(int argc, char* argv[])
{
    UspHandle handle;
    void* context = NULL;
    UspCallbacks testCallbacks;
    uint8_t buffer[MAX_AUDIO_SIZE_IN_BYTE];

    testCallbacks.OnError = OnError;
    testCallbacks.onSpeechEndDetected = OnSpeechEndDetected;
    testCallbacks.onSpeechHypothesis = OnSpeechHypothesis;
    testCallbacks.onSpeechPhrase = OnSpeechPhrase;
    testCallbacks.onSpeechStartDetected = OnSpeechStartDetected;
    testCallbacks.onTurnEnd = OnTurnEnd;
    testCallbacks.onTurnStart = OnTurnStart;

    if (argc < 2)
    {
        printf("Usage: uspclientconsole audio_file");
    }
    
    FILE *audio = fopen(argv[1], "rb");
    if (audio == NULL)
    {
        printf("Error: open file %s failed", argv[1]);
    }

    fread(buffer, sizeof(uint8_t), MAX_AUDIO_SIZE_IN_BYTE, audio);

    UspInitialize(&handle, &testCallbacks, context);

    UspWrite(handle, buffer, sizeof(buffer));

    while (1)
    {
        Sleep(2000);
    }

    UspShutdown(handle);

    fclose(audio);
}
