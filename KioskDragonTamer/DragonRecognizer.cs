using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DNSTools;
using DgnVocTools;
using System.Windows.Forms;
using Microsoft.Psi;
using Microsoft.Psi.Components;
using System.Threading;
using System.IO.Pipes;

namespace NU.Kiosk.Speech
{
    public class DragonRecognizer : IDisposable, IPipeUtilUsers
    {
        // start stop listener
        private string listener_pipe_name;
        private PipeListener listener;

        // text processing sender
        private string destination_pipe_name;
        private PipeSender sender;

        private DgnEngineControl DgnEngine;
        private DgnDictCustom DgnDictCust;
        private DgnMicBtn DgnMicControl;
        private DgnVocTools.DgnVocTools VocTools;

        private TextBox txtEdit;
        private NormalsConfig m_NormalConfig;

        private bool initial_listen_state = true;  // start as not listening, until face is detected.
        private string properNounList;

        public DragonRecognizer(string properNounList = @"Resources\curated_proper_nouns_for_dragon.txt")
        {
            listener_pipe_name = NU.Kiosk.Speech.Program.isDebug ? "dragon_synthesizer_pipe" : "dragon_start_stop_pipe";
            destination_pipe_name = NU.Kiosk.Speech.Program.isDebug ? "dragon_processed_text_pipe" : "dragon_processed_text_pipe";

            this.properNounList = properNounList;

            txtEdit = new TextBox();
            txtEdit.Text = "";
            txtEdit.Enabled = true;
            txtEdit.Focus();

            listener = new PipeListener(ChangeState, listener_pipe_name, this);
            sender = new PipeSender(destination_pipe_name);
        }

        public void Dispose()
        {
            Console.WriteLine("[DragonSpeechSynthesizer] Dispose");

            listener.Dispose();
            sender.Dispose();

            uninitialize();
        }

        public void ReconnectSenders()
        {
            sender.Reconnect();
        }

        public void Initialize()
        {
            //text_proc.Initialize();
            Console.WriteLine($"[DRAGON] Text Processor inialized.");
            if (initializeEngine())
            {
                // Show current speaker name and its training status
                Console.WriteLine($"[DRAGON] Current speaker: {DgnEngine.Speaker}");

                // Disable compatibility modules
                // use -1 as appWnd handle to communicate with Dragon, this setting is saved in 
                // C:\Users\<UserName>\AppData\Roaming\Nuance\NaturallySpeaking15\nsuser.ini
                //DgnEngine.set_CompatibilityModule(DgnCompatibilityModuleConstants.dgncompmoduleAll, -1, false);
                DgnEngine.set_CompatibilityModule(DgnCompatibilityModuleConstants.dgncompmoduleNatText, -1, false);
                DgnEngine.set_CompatibilityModule(DgnCompatibilityModuleConstants.dgncompmoduleNlp, -1, false);
                DgnEngine.set_CompatibilityModule(DgnCompatibilityModuleConstants.dgncompmoduleTracking, -1, false);                
                DgnEngine.set_Option(DgnEngineOptionConstants.dgnengoptionUseConfidence, true);
                DgnEngine.set_Option(DgnEngineOptionConstants.dgnengoptionUseActiveAccessibility, false);

                // If speaker is not loaded offer to select it and load
                // or create new if there are no speakers
                if (DgnEngine.Speaker.Length == 0)
                {
                    throw new Exception("[DRAGON] Speaker is not set...");
                }
                else
                {
                    // Speaker is loaded, and we can initialize the dictation.
                    initialize();
                    DgnEngine.RecognitionMode = DNSTools.DgnRecognitionModeConstants.dgnrecmodeDictation;
                    Console.WriteLine($"[DRAGON] Engine initialized.");
                }
            }            
        }
        
        public void Stop()
        {
            // do nothing
        }


        #region DragonRecognizer Dragon Utilities
        public void trainPhraseAudio(string directory_path, string dictionary_file_name)
        {
            if (DgnEngine.SpeakerModified)
            {
                DgnEngine.SpeakerSave();
            }    

            var dir_path = directory_path.EndsWith("\\") || directory_path.EndsWith("/") ? directory_path.Substring(0, directory_path.Length - 1) : directory_path;
            dir_path += "\\";
            Console.WriteLine($"[DragonRecognizer] training phrase-audio inputs from {dir_path}{dictionary_file_name}...");
            List<string[]> pairs = GetAllStringPairs(dir_path + dictionary_file_name);

            var DgnAdapt1 = new DNSTools.DgnAdapt();
            var strings = new DNSTools.DgnStrings();

            foreach (string[] pair in pairs)
            {
                Console.WriteLine($"[DragonRecognizer] queuing {pair[0]} - {pair[1]}...");
                strings.Add(dir_path + pair[0]); // text 
                strings.Add(dir_path + pair[1]); // audio
            }

            DgnAdapt1.EyesFreeEnroll.Run(strings, false, 0, false);
            Console.WriteLine($"[DragonRecognizer] Training done. {pairs.Count} Pairs trained.");
        }

        public void trainWordsFromWordList(string word_list_file_path)
        {
            IDgnVocabularyBuilder builder = VocTools.VocabularyBuilder;
            IDgnWordAnalyzer analyzer = VocTools.WordAnalyzer;
            IDgnWords newWords = VocTools.WordAnalyzer.CreateWordList();

            Console.WriteLine($"[DRAGON] Training word sequence from {word_list_file_path}... ");

            List<string[]> pairs = GetAllStringPairs(word_list_file_path);
            foreach (string[] pair in pairs)
            {
                var trim = pair[0].Trim();
                newWords.Add(analyzer.AnalyzeWord(trim, trim));
            }

            if (newWords.Count > 0)
            {
                var trained = builder.TrainWords(newWords);
                Console.WriteLine(trained ? "[DRAGON] Training starting." : "[DRAGON] Training dialog failed to start.");
            }
            else
            {
                Console.WriteLine($"[DRAGON] No word list is provided.");
            }
        }

        private void initialize()
        {
            initializeDictation();
            initializeVocTools();
            initMicControl();
            initializePipeServer();
        }

        private void initializePipeServer()
        {
            new Task(() => listener.Initialize()).Start();
            new Task(() => sender.Initialize()).Start();
        }

        private void ChangeState(string res)
        {
            if (res.Equals("1"))
            {
                Console.WriteLine($"[DragonRecognizer] set accepting");
                setAccepting();
            }
            else if (res.Equals("0"))
            {
                Console.WriteLine($"[DragonRecognizer] set not accepting");
                setNotAccepting();
            } 
        }
        
        private void uninitialize()
        {
            uninitializeDictation();
            uninitializeVocTools();
            uninitMicControl();
        }

        private void initMicControl()
        {
            DgnMicControl = new DgnMicBtn();
            DgnMicControl.Register(DgnRegisterConstants.dgnregNoTrayMic);
            if (DgnMicControl.Paused)
            {
                ((IDgnMicBtn)DgnMicControl).MicState = DgnMicStateConstants.dgnmicResume;
            }
        }

        private void uninitMicControl()
        {
            if (DgnMicControl.Paused)
            {
                ((IDgnMicBtn)DgnMicControl).MicState = DgnMicStateConstants.dgnmicResume;
            }
            DgnMicControl.UnRegister();
        }

        private bool initializeEngine()
        {
            try
            {
                // Create a Dragon NaturallySpeaking engine control
                DgnEngine = new DgnEngineControl();
                DgnEngine.Register(DgnRegisterConstants.dgnregNone);
            }
            catch (Exception exc)
            {
                reportError(exc, true);
                return false;
            }
            return true;
        }

        private bool initializeDictation()
        {
            try
            {
                // Create a Dragon NaturallySpeaking custom dictation control
                DgnDictCust = new DgnDictCustom();
                DgnDictCust.Register(DgnRegisterConstants.dgnregNoTrayMic);
                DgnDictCust.set_Option(DgnDictationOptionConstants.dgndictoptionResetDeletesAudio, true);
                DgnDictCust.Active = initial_listen_state;

                //Add DgnDictCustom event handlers
                DgnDictCust.MakeChanges += new _DDgnDictCustomEvents_MakeChangesEventHandler(DgnDictCust_MakeChanges);
            }
            catch (Exception exc)
            {
                reportError(exc, true);
                return false;
            }
            return true;
        }

        private void uninitializeDictation()
        {
            if (DgnDictCust != null)
            {
                DgnDictCust.MakeChanges -= DgnDictCust_MakeChanges;

                // Unregister DgnDictCustom object
                DgnDictCust.UnRegister();
                DgnDictCust = null;
            }
        }

        private void initializeVocTools()
        {
            try
            {
                // Create Dragon Vocabulary Tools object and
                // initialize it for current speaker
                VocTools = new DgnVocTools.DgnVocTools();
                VocTools.Initialize("", "");

                AddProperNouns(properNounList);

                // Load normals for current speaker if this option is allowed
                //// UNUSED
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
        }

        private List<string[]> GetAllStringPairs(string inputPath)
        {

            List<string[]> output = new List<string[]>();
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(inputPath);
            } catch (Exception e)
            {
                Console.WriteLine($"[DRAGON] Cannot open file on path: \"{inputPath}\"; Message: {e.Message}");                
                return new List<string[]>();
            }
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length <= 1 || trimmed.StartsWith("#"))
                {
                    continue;
                }
                else if (trimmed.StartsWith("-"))
                {
                    var toBeDeleted = trimmed.Substring(1);
                    Console.WriteLine($"[DRAGON] word removal is not yet implemented; term: \"{toBeDeleted}\"");
                    continue;
                }
                else
                {
                    string[] split = trimmed.Split('|');
                    if (split.Length == 2 || split.Length == 1)
                    {
                        output.Add(split);

                    }
                    else
                    {
                        Console.WriteLine($"[DRAGON] cannot parse proper name input: \"{trimmed}\"");
                        continue;
                    }
                }
            }
            return output;
        }

        private void AddProperNouns(string inputPath)
        {
            // Add proper names?
            IDgnVocabularyBuilder builder = VocTools.VocabularyBuilder;
            IDgnWordAnalyzer analyzer = VocTools.WordAnalyzer;
            IDgnWords newWords = VocTools.WordAnalyzer.CreateWordList();

            //builder.RemoveWords(newWords); // USE WITH CAUTION; once a word is removed, it must be re-added with "DiscernWords", instead of "AddWords"

            List<string[]> pairs = GetAllStringPairs(inputPath);
            foreach (string[] pair in pairs)
            {
                newWords.Add(analyzer.AnalyzeWord(pair[0].Trim(), pair[1].Trim()));
            }

            if (newWords.Count > 0)
            {
                Console.Write($"[DRAGON] WordFreeSpace: {builder.WordFreeSpace}; updating vocabulary with {newWords.Count} words... ");
                var addedWords = builder.AddWords(newWords, 1);
                Console.WriteLine($"{addedWords.Count} new words added.");
            }
            else
            {
                Console.WriteLine($"[DRAGON] No word list is provided.");
            }
        }

        private void uninitializeVocTools()
        {
            try
            {
                if (VocTools != null)
                {
                    VocTools.Uninitialize();
                    VocTools = null;
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
        }

        void DgnDictCust_MakeChanges(ref int Start, ref int NumChars,
            ref string Text, ref int SelStart, ref int SelNumChars)
        {
            setNotAccepting();
            var t = new String(Text.ToCharArray());
            //this.Out.Post(t, DateTime.Now);
            new Task( () => sender.Send(t)).Start();
            Console.WriteLine($"[DRAGON] Generated text: '{t}'");
            resetText();
            //DateTime TimerStart = DateTime.Now;
            //TimerStartTime = TimerStart.DeepClone();
            //restartAcceptingInMs(10000, TimerStart.DeepClone());
        }

        private void resetText()
        {
            if (DgnDictCust != null)
            {
                DgnDictCust.Reset();
            }
            txtEdit.Clear();
        }

        static void reportError(Exception exc, bool printStackTrace)
        {
            Console.WriteLine($"[DRAGON] Error {exc.Message}");
            if (!printStackTrace)
            {
                Console.WriteLine($"[DRAGON] StackTrace:");
                foreach (string s in exc.StackTrace.Split('\n'))
                {
                    Console.WriteLine($"{s}");
                }
            }
        }
        #endregion

        #region DragonRecognizer Normals

        public struct WordsPair
        {
            public WordsPair(String sNormal, String sExpanded)
            {
                sNormalText = sNormal;
                sExpandedText = sExpanded;
            }

            public String sNormalText;
            public String sExpandedText;
        }

        public class NormalsConfig
        {
            public NormalsConfig()
            {
                wpNormals = new List<WordsPair>();
                wpTriggers = new List<WordsPair>();
                bExpandNormals = false;
            }

            // Copy constructor
            public NormalsConfig(NormalsConfig Config)
            {
                bExpandNormals = Config.bExpandNormals;

                wpTriggers = new List<WordsPair>();
                foreach (WordsPair Trigger in Config.wpTriggers)
                {
                    wpTriggers.Add(Trigger);
                }

                wpNormals = new List<WordsPair>();
                foreach (WordsPair Normal in Config.wpNormals)
                {
                    wpNormals.Add(Normal);
                }
            }

            // NormalsConfig members
            public List<WordsPair> wpNormals;
            public List<WordsPair> wpTriggers;
            public bool bExpandNormals;
        }

        private bool isNormalsAllowed()
        {
            bool Allowed = false;
            try
            {
                if (VocTools != null)
                {
                    Allowed = VocTools.get_IsAllowed(DgnFunctionalityConstants.dgnfDictationNormals);
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
            return Allowed;
        }

        private DgnStrings createDeletedWordsCollection(NormalsConfig newConfig, bool bNormals)
        {
            DgnStrings dgnStrings = new DgnStrings();
            IDgnNormalWordManager wordManager = VocTools.VocabularyBuilder as IDgnNormalWordManager;

            // Get references for corresponding lists
            List<WordsPair> oldWords = bNormals ? m_NormalConfig.wpNormals : m_NormalConfig.wpTriggers;
            List<WordsPair> newWords = bNormals ? newConfig.wpNormals : newConfig.wpTriggers;
            foreach (WordsPair oldWord in oldWords)
            {
                // Check if this word was deleted from previous configuration
                bool bDeleted = true;
                foreach (WordsPair word in newWords)
                {
                    if (oldWord.sNormalText == word.sNormalText)
                    {
                        bDeleted = false;
                        break;
                    }
                }
                if (bDeleted)
                {
                    // This word is deleted. It should be removed from VocTools
                    // Check if it exists there.
                    bool bExists = bNormals ? wordManager.IsNormalWord(oldWord.sNormalText) :
                        wordManager.IsNormalTriggerWord(oldWord.sNormalText);
                    if (bExists)
                    {
                        dgnStrings.Add(oldWord.sNormalText);
                    }
                }
            }

            return dgnStrings;
        }

        private DgnStrings createAddedWordsCollection(NormalsConfig newConfig, bool bNormals)
        {
            DgnStrings dgnStrings = new DgnStrings();
            IDgnNormalWordManager wordManager = VocTools.VocabularyBuilder as IDgnNormalWordManager;

            // Get references for corresponding lists
            List<WordsPair> oldWords = bNormals ? m_NormalConfig.wpNormals : m_NormalConfig.wpTriggers;
            List<WordsPair> newWords = bNormals ? newConfig.wpNormals : newConfig.wpTriggers;
            foreach (WordsPair newWord in newWords)
            {
                // Check if this word was added to the new configuration
                bool bAdded = true;
                foreach (WordsPair word in oldWords)
                {
                    if (newWord.sNormalText == word.sNormalText)
                    {
                        bAdded = false;
                        break;
                    }
                }
                if (bAdded)
                {
                    // This word is new. It should be added to VocTools
                    // Check if it doesn't exist there.
                    bool bExists = bNormals ? wordManager.IsNormalWord(newWord.sNormalText) :
                        wordManager.IsNormalTriggerWord(newWord.sNormalText);
                    if (!bExists)
                    {
                        dgnStrings.Add(newWord.sNormalText);
                    }
                }
            }

            return dgnStrings;
        }

        private void loadNormals(DgnStrings dgnTriggerWords, DgnStrings dgnNormalWords)
        {
            IDgnNormalWordManager wordManager = VocTools.VocabularyBuilder as IDgnNormalWordManager;

            try
            {
                // Load Trigger words
                foreach (String sTrigger in dgnTriggerWords)
                {
                    try
                    {
                        wordManager.SetNormalTriggerWord(sTrigger);
                    }
                    catch (Exception)
                    {
                        reportError(new Exception("Failed to add '" + sTrigger + "' trigger word"), false);
                    }
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }

            try
            {
                // Load Normal words
                if (dgnNormalWords.Count > 0)
                {
                    wordManager.AddNormalWords(dgnNormalWords);
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
        }

        private void unloadNormals(DgnStrings dgnTriggerWords, DgnStrings dgnNormalWords)
        {
            IDgnNormalWordManager wordManager = VocTools.VocabularyBuilder as IDgnNormalWordManager;

            try
            {
                // Unload Trigger words
                foreach (String sTrigger in dgnTriggerWords)
                {
                    try
                    {
                        wordManager.RemoveNormalTriggerWord(sTrigger);
                    }
                    catch (Exception)
                    {
                        reportError(new Exception("Failed to remove '" + sTrigger + "' trigger word"), false);
                    }
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }

            try
            {
                // Unload Normal words
                if (dgnNormalWords.Count > 0)
                {
                    wordManager.RemoveNormalWords(dgnNormalWords);
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
        }

        private void loadSpeakerNormals()
        {
            try
            {
                // Reset normals configuration
                m_NormalConfig = new NormalsConfig();

                IDgnNormalWordManager normalWordMgr = VocTools.VocabularyBuilder as IDgnNormalWordManager;

                // Get Trigger words from the VocTools
                DgnStrings dgnTriggers = normalWordMgr.GetNormalTriggerWords();
                foreach (String sTrigger in dgnTriggers)
                {
                    m_NormalConfig.wpTriggers.Add(new WordsPair(sTrigger, ""));
                }

                // Get Normal words from the VocTools
                DgnStrings dgnNormals = normalWordMgr.GetNormalWords();
                foreach (String sNormal in dgnNormals)
                {
                    m_NormalConfig.wpNormals.Add(new WordsPair(sNormal, ""));
                }
            }
            catch (Exception exc)
            {
                reportError(exc, false);
            }
        }
        #endregion

        #region DragonRecognizer accept
        public void setAccepting()
        {
            if (!NU.Kiosk.Speech.Program.isTraining)
            {
                ((IDgnMicBtn)DgnMicControl).MicState = DgnMicStateConstants.dgnmicResume;
                DgnDictCust.Active = true;
            }
        }

        public void setNotAccepting()
        {
            if (!NU.Kiosk.Speech.Program.isTraining)
            {
                ((IDgnMicBtn)DgnMicControl).MicState = DgnMicStateConstants.dgnmicPause;
                DgnDictCust.Active = false;
            }            
        }
        #endregion
    }

}
