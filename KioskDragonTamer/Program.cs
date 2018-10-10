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
    using NDesk.Options;

    public static class Program
    {
        public static bool isDebug = false;

        public static void Main(string[] args)
        {
            string training_file_directory_path = @"Resources\Audio";
            string training_file_list = @"text_audio_list.txt";
            bool train_phrases = false;

            var p = new OptionSet() {
                { "d|debug", "Stand-alone debug mode. All speeches are consumed locally; recognized speech will be echoed back.",
                   v => isDebug = true
                },
                { "t|train", "Train Dragon with Phrase-Audio pairs. ",
                   v => train_phrases = v != null
                },
                { "train_dir=", "Train Dragon with Phrase-Audio pairs from the following directory. ",
                   v => {
                        training_file_directory_path = v;
                        train_phrases = true;
                   }
                },
                { "train_file_list=", "Train Dragon with Phrase-Audio pairs mentioned in this file.",
                   v => {
                        training_file_list = v;
                        train_phrases = true;
                   }
                },
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `program.exe --help' for more information.");
                return;
            }

            if (train_phrases)
            {
                Train(training_file_directory_path, training_file_list);
            } else
            {
                Run();
            }            
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

        public static void Train(string training_file_directory_path, string training_file_list)
        {
            var recognizer = new DragonRecognizer(training_file_directory_path, training_file_list);
            recognizer.Initialize();
            recognizer.train(training_file_directory_path, training_file_list);
            Console.WriteLine("[KioskDragonTamer.Run] Press any key to exit...");
            Console.ReadKey(true);
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
