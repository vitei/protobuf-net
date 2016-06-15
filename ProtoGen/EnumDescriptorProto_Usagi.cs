using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using google.protobuf;
using System.Runtime.InteropServices;
using System.Dynamic;

using ProtoBuf.CodeGenerator;

namespace google.protobuf
{
    public partial class EnumDescriptorProto
    {
        // generic key/value store to query in xslt later
        public List<NamedList<List<ProtoBuf.CodeGenerator.KeyValuePair<string, string>>>> Metadata = new List<NamedList<List<ProtoBuf.CodeGenerator.KeyValuePair<string, string>>>>();
    }
    public partial class MessageDescriptorProto
    {
        // generic key/value store to query in xslt later
        public List<NamedList<List<ProtoBuf.CodeGenerator.KeyValuePair<string, string>>>> Metadata = new List<NamedList<List<ProtoBuf.CodeGenerator.KeyValuePair<string, string>>>>();
    }
}
