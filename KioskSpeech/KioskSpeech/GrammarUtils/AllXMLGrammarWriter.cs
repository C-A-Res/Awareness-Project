using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Psi.Samples.SpeechSample
{
    class AllXMLGrammarWriter
    {
        static XNamespace defaultNs = "http://www.w3.org/2001/06/grammar";

        private string input_file_path = @"Resources\OriginalGrammar.grxml";
        private string output_file_path = @"Resources\GeneratedGrammar.grxml";
        //Dictionary<string, XElement> rules_one_of = new Dictionary<string, XElement>();
        XElement root;

        public AllXMLGrammarWriter(string input_file_path = @"Resources\OriginalGrammar.grxml")
        {
            this.input_file_path = input_file_path;
            //ReadOriginalXMLFile(this.input_file_path);
        }

        //public void ReadOriginalXMLFile(string input_file_path = @"Resources\OriginalGrammar.grxml")
        //{
        //    // load the base template 
        //    root = XElement.Load(input_file_path);
        //}

        public void ReadFileAndConvert(string file_path = @"Resources\AdditionalGrammarToBeMerged.grxml")
        {
            XElement left = XElement.Load(input_file_path);
            XElement right = XElement.Load(file_path);
            MergeToLeft(left, right);
            Console.WriteLine(left.ToString());
        }

        private void MergeToLeft(XElement left, XElement right)
        {

            var leftElements = left.Elements();
            foreach (XElement e in right.Elements())
            {
                Console.WriteLine("[MergeToLeft] for each e in right: {0}", e);
                IEnumerable<XElement> foundElements = leftElements.Where(
                        (x) => {
                            if (!x.Name.LocalName.Equals(e.Name.LocalName))
                                return false;
                            switch (x.Name.LocalName)
                            {
                                case "rule":
                                    return x.Attribute("id").Value.Equals(e.Attribute("id").Value);
                                case "ruleref":
                                    return x.Attribute("uri").Value.Equals(e.Attribute("uri").Value);
                                case "item":
                                    if (!x.Elements().Any())
                                    {
                                        // compare the texts
                                        if (x.Nodes().Count() != e.Nodes().Count())
                                        {
                                            return false;
                                        }
                                        IEnumerable<XNode> xnodes = x.Nodes();
                                        IEnumerable<XNode> enodes = e.Nodes();
                                        for (int i = 0, c = xnodes.Count(); i < c; i++)
                                        {
                                            if (!xnodes.ElementAt(i).Equals(enodes.ElementAt(i)))
                                            {
                                                return false;
                                            }
                                        }
                                        return true;
                                    }
                                    else
                                    {
                                        return x.Value.Equals(e.Value);
                                    }
                                case "one-of":
                                default:
                                    return true;
                            }
                        });
                if (foundElements.Any())
                {
                    // merge children
                    foreach (XElement el in foundElements)
                    {
                        MergeToLeft(el, e);
                    }
                }
                else
                {
                    // add to left
                    left.Add(e);
                }
            }
        }

        public void WriteToFile(string out_path = null)
        {
            if (out_path == null)
            {
                out_path = output_file_path;
            }
            System.IO.File.WriteAllText(out_path, root.ToString());
            string project_dir = Path.GetDirectoryName(Path.GetDirectoryName(System.IO.Directory.GetCurrentDirectory()));
            System.IO.File.WriteAllText(project_dir + "\\" + out_path, root.ToString());
        }

        public string GetResultString()
        {
            return root.ToString();
        }

        public void Print()
        {
            Console.WriteLine(root.ToString());
        }

        public string Input_file_path { get => input_file_path; set => input_file_path = value; }
        public string Output_file_path { get => output_file_path; set => output_file_path = value; }
    }
}
