using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ProtoBuf.CodeGenerator
{
    [Serializable]
    [XmlType(TypeName = "NamedList")]
    public struct NamedList<L>
    {
        public string Name;
        public L List;

        public NamedList(string v1, L v2) : this()
        {
            this.Name = v1;
            this.List = v2;
        }
    }

    [Serializable]
    [XmlType(TypeName = "KeyValuePair")]
    public struct KeyValuePair<K, V>
    {
        public KeyValuePair(K v1, V v2) : this()
        {
            this.Key = v1;
            this.Value = v2;
        }

        public K Key;
        public V Value;
    }
}
