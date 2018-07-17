using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NU.Kiosk.Speech
{
    class GrammarWriter
    {
        static XNamespace defaultNs = "http://www.w3.org/2001/06/grammar";

        private string input_file_path = @"Resources\CuratedGrammar.grxml";
        private string output_file_path = @"Resources\GeneratedGrammar.grxml";
        Dictionary<string, XElement> rules_one_of = new Dictionary<string, XElement>();
        XElement root;

        public GrammarWriter(string input_file_path = @"Resources\CuratedGrammar.grxml")
        {
            this.input_file_path = input_file_path;
            ReadXMLFile(this.input_file_path);
        }

        public void ReadXMLFile(string input_file_path = @"Resources\CuratedGrammar.grxml")
        {
            // load the base template 
            root = XElement.Load(input_file_path);

            // add pointer to each XML element
            IEnumerable<XElement> children = root.Elements();
            rules_one_of = new Dictionary<string, XElement>();
            foreach (XElement desc in children)
            {
                string name = desc.Attribute("id").Value;
                if (desc.Elements().First().Name.LocalName == "one-of")
                {
                    rules_one_of.Add(name, desc.Elements().First());
                } else 
                {
                    bool found = false;
                    foreach (XElement e in desc.Elements())
                    {
                        if (e.Name.LocalName == "one-of")
                        {
                            found = true;
                            rules_one_of.Add(name, e);
                            break;
                        }
                    }
                    if (!found)
                    {
                        rules_one_of.Add(name, desc);
                    }
                    
                }
            }
        }

        public void AddToExistingRule(string rule_name, params string[] contents)
        {
            XElement new_item = new XElement(defaultNs + "item");
            for (int i = 0, l = contents.Length; i < l; i++)
            {
                if (contents[i].StartsWith("#"))
                {
                    XElement rref = new XElement(defaultNs + "ruleref");
                    rref.Add(new XAttribute("uri", contents[i]));
                    new_item.Add(rref);
                } else
                {
                    new_item.Add(new XText(contents[i]));
                }
            }
            rules_one_of[rule_name].Add(new_item);
        }

        public bool CheckIfItemExists(string rule_name, params string[] contents)
        {
            var items = rules_one_of[rule_name].Elements();
            foreach (var item in items)
            {
                IEnumerable<XNode> item_elements = item.Nodes();
                if (item_elements.Count() != contents.Length)
                {
                    continue;
                }
                for (int i = 0, l = contents.Length; i < l; i++)
                {
                    XNode ei = item_elements.ElementAt(i);
                    if (ei.GetType() == typeof(XText))
                    {
                        if (!((XText)ei).Value.Equals(contents[i]))
                        {
                            return false;
                        }
                    } else
                    {
                        // assume it is ruleref
                        if (!((XElement)ei).Attribute("uri").Value.Equals(contents[i]))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public void CreateNewRuleIfNotExists(string rule_name)
        {
            if (!rules_one_of.ContainsKey(rule_name))
            {
                XElement new_rule = new XElement(defaultNs + "rule");
                new_rule.Add(new XAttribute("id", rule_name));
                XElement one_of = new XElement(defaultNs + "one-of");
                new_rule.Add(one_of);
                root.Add(new_rule);
                rules_one_of.Add(rule_name, one_of);
            }            
        }

        public void ReadFileAndConvert(string file_path = @"Resources\primitive_grammar.txt")
        {
            string[] lines = File.ReadAllLines(file_path);

            string currentRuleName = null;

            foreach (string line in lines)
            {
                if (!line.StartsWith("\t"))
                {
                    currentRuleName = line.Trim();
                    CreateNewRuleIfNotExists(currentRuleName);
                }
                else
                {
                    if (currentRuleName == null)
                        throw new Exception("Item not associated with rule: " + line);

                    string[] split = ItemLineSplit(line);
                    if (!CheckIfItemExists(currentRuleName, split))
                    {
                        AddToExistingRule(currentRuleName, split);
                    }                    
                }
            }
        }

        private static string[] ItemLineSplit(string untrimmed)
        {
            string[] untrimmed_array = untrimmed.TrimStart('\t').Split('|');
            if (untrimmed_array.Length == 0)
                return untrimmed_array;

            List<string> output_list = new List<string>();

            for (int i = 0, l = untrimmed_array.Length; i < l; i++)
            {
                if (untrimmed_array[i].Length > 0)
                    if (untrimmed_array[i].Contains("#"))
                        output_list.Add(untrimmed_array[i].Trim());
                    else
                        output_list.Add(untrimmed_array[i]);
            }

            return output_list.ToArray();
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
