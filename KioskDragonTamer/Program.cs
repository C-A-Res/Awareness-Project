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

        public static void Main(string[] args)
        {
            Run();
        }

        public static void Run()
        {
            var recognizer = new DragonRecognizer();
            var speechSynth = new DragonSpeechSynthesizer(recognizer);

            recognizer.Initialize();
            Console.WriteLine("[KioskDragonTamer.Run] Recognizer initialized.");
            speechSynth.Initialize();
            Console.WriteLine("[KioskDragonTamer.Run] Synthesizer initialized.");

            Console.WriteLine("[KioskDragonTamer.Run] Press any key to exit...");
            Console.ReadKey(true);
            // must go in this order
            speechSynth.Dispose();
            recognizer.Dispose();
        }        

        public static void speechTester()
        {
            Console.WriteLine("[KioskDragonTamer.Run] Enter 'exit' to exit...");
            string input;
            var recognizer = new DragonRecognizer();
            DragonSpeechSynthesizer speechSynth = new DragonSpeechSynthesizer(recognizer);
            while ((input = Console.ReadLine()).ToLower() != "exit")
            {
                if (input.Trim().Length > 0)
                {
                    Console.WriteLine($"[KioskDragonTamer.Run] '{input}'");
                    speechSynth.Speak(input);
                }
                else
                {
                    Console.WriteLine($"[KioskDragonTamer.Run] Come again?");
                }
            }
        }

    }
}
