using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NU.Kiosk.SharedObject
{
    public class Action
    {
        public string Name { get; private set; }
        public List<Object> Args { get; private set; }

        public Action(string name, params Object[] args)
        {
            Name = name;
            Args = new List<object>(args);
        }

        public string toKQML()
        {
            var sb = new System.Text.StringBuilder('(');
            sb.Append(Name);
            foreach (var x in Args)
            {
                sb.Append(' ').Append(x.ToString());
            }
            sb.Append(')');
            return sb.ToString();
        }

        public override string ToString()
        {
            return toKQML();
        }
    }
}
