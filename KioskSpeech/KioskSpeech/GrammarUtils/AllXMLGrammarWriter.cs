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

namespace NU.Kiosk
{
    class AllXMLGrammarWriter
    {
        static XNamespace defaultNs = "http://www.w3.org/2001/06/grammar";

        private string input_file_path = @"Resources\BaseGrammar.grxml";
        private string output_file_path = @"Resources\BaseGrammar.grxml";
        //Dictionary<string, XElement> rules_one_of = new Dictionary<string, XElement>();
        XElement root;

        public AllXMLGrammarWriter(string input_file_path = @"Resources\BaseGrammar.grxml")
        {
            this.input_file_path = input_file_path;
        }
        
        public void ReadFileAndConvert(string file_path = @"Resources\AdditionalGrammarToBeMerged.grxml")
        {
            XElement left = XElement.Load(input_file_path);
            XElement right = XElement.Load(file_path);
            MergeToLeft(left, right);
            root = left;
            Console.WriteLine($"[ReadFileAndConvert] Merged grammar from '{file_path}' to '{input_file_path}'.");
        }

        private void MergeToLeft(XElement left, XElement right)
        {

            var leftElements = left.Elements();
            foreach (XElement e in right.Elements())
            {
                //Console.WriteLine("[MergeToLeft] for each e in right: {0}", e);
                IEnumerable<XElement> foundElements = leftElements.Where(
                        (x) => {
                            return isRightPartOfLeft(x, e);
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

        private Boolean isRightPartOfLeft(XElement left, XElement right)
        {
            if (!left.Name.LocalName.Equals(right.Name.LocalName))
            {
                return false;
            }                
            switch (left.Name.LocalName)
            {
                case "rule":
                    return left.Attribute("id").Value.Equals(right.Attribute("id").Value);
                case "ruleref":
                    return left.Attribute("uri").Value.Equals(right.Attribute("uri").Value);
                case "item":
                    return XNode.DeepEquals(left, right);
                case "one-of":
                    return true;
                default:
                    return true;
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
            Console.WriteLine($"[WriteToFile] Updated grammar written to {out_path}.");
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
