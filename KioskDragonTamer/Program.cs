// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corporation">
//   Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NU.Kiosk.Speech
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Speech;
    using Microsoft.Psi.Visualization.Client;
    using System.Speech.Recognition;
    using System.Threading.Tasks;

    public static class Program
    {
        private const string AppName = "PsiSpeechSample";
        private const string LogPath = @"..\..\..\Videos\" + Program.AppName;
        private const bool isUsingDragon = true;
        private static int ReloadMessageIDCurrent = 0;
        

        public static void Main(string[] args)
        {
            Run();
        }

        public static void Run()
        {
            var recognizer = new DragonRecognizer();
            var speechSynth = new DragonSpeechSynthesizer(recognizer);
            new Task(() => recognizer.Initialize()).Start();
            Console.WriteLine("Recognizer initialized.");
            new Task(() => speechSynth.Initialize()).Start();
            Console.WriteLine("Synthesizer initialized.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
            // must go in this order
            speechSynth.Dispose();
            recognizer.Dispose();
        }        

        public static void speechTester()
        {
            Console.WriteLine("Enter 'exit' to exit...");
            string input;
            var recognizer = new DragonRecognizer();
            DragonSpeechSynthesizer speechSynth = new DragonSpeechSynthesizer(recognizer);
            while ((input = Console.ReadLine()).ToLower() != "exit")
            {
                if (input.Trim().Length > 0)
                {
                    Console.WriteLine($"[Program.cs] '{input}'");
                    speechSynth.Speak(input);
                }
                else
                {
                    Console.WriteLine($"[Program.cs] Come again?");
                }
            }
        }

    }
}
