using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Xsl;
using google.protobuf;
using System.Xml;
using System.Text;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Diagnostics;

using XmlSerializer = System.Xml.Serialization.XmlSerializer;
using System.Reflection;
using System.Linq;

namespace ProtoBuf.CodeGenerator
{
    public sealed class CommandLineOptions
    {
        private TextWriter errorWriter = Console.Error;
        private string workingDirectory = Environment.CurrentDirectory;

        /// <summary>
        /// Root directory for the session
        /// </summary>
        public string WorkingDirectory
        {
            get { return workingDirectory;}
            set { workingDirectory = value; }
        }

        /// <summary>
        /// Nominates a writer for error messages (else stderr is used)
        /// </summary>
        public TextWriter ErrorWriter
        {
            get { return errorWriter; }
            set { errorWriter = value; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Main(params string[] args)
        {
            CommandLineOptions opt = null;
            try
            {
                opt = Parse(Console.Out, args);
                opt.Execute();
                return opt.ShowHelp ? 1 : 0; // count help as a non-success (we didn't generate code)
            }
            catch (Exception ex)
            {
                Console.Error.Write(ex.Message);
                return 1;
            }
        }

        private string template = TemplateCSharp, outPath = "", defaultNamespace;
        private bool showLogo = true, showHelp, writeErrorsToFile;
        private readonly List<string> inPaths = new List<string>();
        private readonly List<string> refPaths = new List<string>();
        private readonly List<string> args = new List<string>();

        private int messageCount;
        public int MessageCount { get { return messageCount; } }
        public bool WriteErrorsToFile { get { return writeErrorsToFile; } set { writeErrorsToFile = value; } }
        public string Template { get { return template;} set { template = value;} }
        public string DefaultNamespace { get { return defaultNamespace; } set { defaultNamespace = value; } }
        public bool ShowLogo { get { return showLogo; } set { showLogo = value; } }
        public string OutPath { get { return outPath; } set { outPath = value; } }
        public bool ShowHelp { get { return showHelp; } set { showHelp = value; } }
        private readonly XsltArgumentList xsltOptions = new XsltArgumentList();
        public XsltArgumentList XsltOptions { get { return xsltOptions; } }
        public List<string> InPaths { get { return inPaths; } }
        public List<string> RefPaths { get { return refPaths; } }
        public List<string> Arguments { get { return args; } }

        public static Assembly DLLAssembly { get; private set; }

        private readonly TextWriter messageOutput;

        public static CommandLineOptions Parse(TextWriter messageOutput, params string[] args)
        {
            CommandLineOptions options = new CommandLineOptions(messageOutput);

            string key, value;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim();
                if (arg.StartsWith("-o:"))
                {
                    if (!string.IsNullOrEmpty(options.OutPath)) options.ShowHelp = true;
                    options.OutPath = arg.Substring(3).Trim();
                }
                else if (arg.StartsWith("-dll:"))
                {
                    DLLAssembly = Assembly.LoadFrom(arg.Substring(5));                    
                }
                else if (arg.StartsWith("-p:"))
                {
                    Split(arg.Substring(3), out key, out value);
                    options.XsltOptions.AddParam(key, "", value ?? "true");
                }
                else if (arg.StartsWith("-t:"))
                {
                    options.Template = arg.Substring(3).Trim();
                }
                else if (arg.StartsWith("-ns:"))
                {
                    options.DefaultNamespace = arg.Substring(4).Trim();
                }
                else if (arg == "/?" || arg == "-h")
                {
                    options.ShowHelp = true;
                }
                else if (arg == "-q") // quiet
                {
                    options.ShowLogo = false;
                }
                else if (arg == "-d")
                {
                    options.Arguments.Add("--include_imports");
                }
                else if (arg.StartsWith("-i:"))
                {
                    options.InPaths.Add(arg.Substring(3).Trim());
                }
                else if (arg.StartsWith("-r:"))
                {
                    string path = arg.Substring(3).Trim();
                    options.RefPaths.Add(path);
                    options.Arguments.Add("--proto_path=" + path);
                }
                else if (arg == "-writeErrors")
                {
                    options.WriteErrorsToFile = true;
                }
                else if (arg.StartsWith("-w:"))
                {
                    options.WorkingDirectory = arg.Substring(3).Trim();
                }
                else
                {
                    options.ShowHelp = true;
                }
            }
            if (options.InPaths.Count == 0)
            {
                options.ShowHelp = (string) options.XsltOptions.GetParam("help", "") != "true";
            }
            return options;

        }

        static readonly char[] SplitTokens = { '=' };
        private static void Split(string arg, out string key, out string value)
        {
            string[] parts = arg.Trim().Split(SplitTokens, 2);
            key = parts[0].Trim();
            value = parts.Length > 1 ? parts[1].Trim() : null;
        }

        public CommandLineOptions(TextWriter messageOutput)
        {
            if(messageOutput == null) throw new ArgumentNullException("messageOutput");
            this.messageOutput = messageOutput;

            // handling this (even trivially) suppresses the default write;
            // we'll also use it to track any messages that are generated
            XsltOptions.XsltMessageEncountered += delegate { messageCount++; };
        }

        public const string TemplateCSharp = "csharp";

        private string code;
        public string Code { get { return code; } private set { code = value; } }
        
        public void Execute()
        {
            StringBuilder errors = new StringBuilder();
            string oldDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = WorkingDirectory;
            try
            {
                if (string.IsNullOrEmpty(OutPath))
                {
                    WriteErrorsToFile = false; // can't be
                }
                else if (WriteErrorsToFile)
                {
                    ErrorWriter = new StringWriter(errors);
                }
                try
                {
                    if (ShowLogo)
                    {
                        messageOutput.WriteLine(Properties.Resources.LogoText);
                    }
                    if (ShowHelp)
                    {
                        messageOutput.WriteLine(Properties.Resources.Usage);
                        return;
                    }

                    string xml = LoadFilesAsXml(this);
                    Code = ApplyTransform(this, xml);
                    if (this.OutPath == "-") { }
                    else if (!string.IsNullOrEmpty(this.OutPath))
                    {
                        File.WriteAllText(this.OutPath + ".xml", xml);
                        File.WriteAllText(this.OutPath, Code);
                    }
                    else if (string.IsNullOrEmpty(this.OutPath))
                    {
                        messageOutput.Write(Code);
                    }
                }
                catch (Exception ex)
                {
                    if (WriteErrorsToFile)
                    {
                        // if we had a parse fail and were able to capture something
                        // sensible, then just write that; otherwise use the exception
                        // as well
                        string body = (ex is ProtoParseException && errors.Length > 0) ?
                            errors.ToString() : (ex.Message + Environment.NewLine + errors);
                        File.WriteAllText(this.OutPath, body);
                    }
                    throw;
                }
            }
            finally
            {
                try { Environment.CurrentDirectory = oldDir; }
                catch (Exception ex) { Trace.WriteLine(ex); }
            }
        }


        private static string LoadFilesAsXml(CommandLineOptions options)
        {
            FileDescriptorSet set = new FileDescriptorSet();

            foreach (string inPath in options.InPaths)
            {
                InputFileLoader.Merge(set, inPath, options.ErrorWriter, options.Arguments.ToArray());
            }

            HashSet<string> seenPaths = new HashSet<string>(options.InPaths);

            HashSet<string> depPaths = new HashSet<string>();
            foreach (FileDescriptorProto file in set.file)
            {
                depPaths.UnionWith(file.dependency);
            }

            while(depPaths.Count != 0)
            {
                foreach (string inPath in depPaths)
                {
                    string path = inPath;
                    if(!File.Exists(path))
                    {
                        foreach (string searchPath in options.RefPaths)
                        {
                            path = searchPath + "\\" + inPath;
                            if(File.Exists(path))
                            {
                                break;
                            }
                        }
                    }

                    InputFileLoader.Merge(set, path, options.ErrorWriter, options.Arguments.ToArray());
                }

                seenPaths.UnionWith(depPaths);
                depPaths = new HashSet<string>();
                foreach (FileDescriptorProto file in set.file)
                {
                    depPaths.UnionWith(file.dependency);
                }
                depPaths.ExceptWith(seenPaths);
            }

            // If we've got an assembly loaded from a DLL, get the classes
            // within that implement IExtensible.
            List<Type> extensionTypes = null;
            if (CommandLineOptions.DLLAssembly != null)
            {
                extensionTypes = DLLAssembly.GetExportedTypes()
                    .ToList()
                    .Where(t => t.IsClass == true)
                    .Where(t => typeof(global::ProtoBuf.IExtensible).IsAssignableFrom(t))
                    .ToList();
            }

            foreach (var file in set.file)
            {
                foreach (var et in file.enum_type)
                {
                    if (et.options == null)
                        continue;
                    
                    foreach (var extType in extensionTypes)
                    {
                        var thisExtension = Activator.CreateInstance(extType);

                        // @todo replace me! we should really be passing the
                        // field numbers along with the DLL
                        int key = 0;
                        if (extType.Name.Equals("NanoPBOptions"))
                        {
                            key = 1010;
                        }
                        else if (extType.Name.Equals("UsagiPBEnumOptions"))
                        {
                            key = 1011;
                        }
                        if (key != 0)
                        {
                            MethodInfo method = typeof(Extensible).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name.Equals("TryGetValue")).First();
                            MethodInfo generic = method.MakeGenericMethod(extType);
                            var args = new object[]
                            {
                                et.options,
                                key,
                                thisExtension
                            };
                            bool bSuccess = (bool)generic.Invoke(null, args);
                            if (bSuccess)
                            {
                                thisExtension = args[2];
                                var keys = new NamedList<List<KeyValuePair<string, string>>>(extType.Name, new List<KeyValuePair<string, string>>());
                                foreach (var item in extType.GetProperties())
                                {
                                    keys.List.Add(new KeyValuePair<string, string>(
                                        item.Name,
                                        item.GetValue(thisExtension).ToString()
                                    ));
                                }
                                et.Metadata.Add(keys);
                            }
                        }
                    }
                }
            }            

            XmlSerializer xser = new XmlSerializer(
                typeof(FileDescriptorSet),
                new Type[] {
                    typeof (KeyValuePair<string, string>),
                    typeof (List<KeyValuePair<string, string>>),
                    typeof (List<NamedList<List<KeyValuePair<string, string>>>>)
                }
            );
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.NewLineHandling = NewLineHandling.Entitize;
            StringBuilder sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                xser.Serialize(writer, set);
            }
            return sb.ToString();
        }

        private static string ApplyTransform(CommandLineOptions options, string xml)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Auto;
            settings.CheckCharacters = false;
            
            StringBuilder sb = new StringBuilder();
            using (XmlReader reader = XmlReader.Create(new StringReader(xml)))
            using (TextWriter writer = new StringWriter(sb))
            {
                XslCompiledTransform xslt = new XslCompiledTransform();
                string xsltTemplate = Path.ChangeExtension(options.Template, "xslt");
                if (!File.Exists(xsltTemplate))
                {
                    string localXslt = InputFileLoader.CombinePathFromAppRoot(xsltTemplate);
                    if (File.Exists(localXslt))
                        xsltTemplate = localXslt;
                }
                try
                {
                    xslt.Load(xsltTemplate);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unable to load tranform: " + options.Template, ex);
                }
                options.XsltOptions.RemoveParam("defaultNamespace", "");
                if (options.DefaultNamespace != null)
                {
                    options.XsltOptions.AddParam("defaultNamespace", "", options.DefaultNamespace);
                }
                xslt.Transform(reader, options.XsltOptions, writer);
            }

            string Code = sb.ToString();
            int firstFileIndex = Code.IndexOf("// Generated from:");
            if (firstFileIndex != -1)
            {
                int secondFileIndex = Code.IndexOf("// Generated from:", firstFileIndex + 1);
                if (secondFileIndex != -1)
                {
                    Code = Code.Substring(0, secondFileIndex);
                }
            }
            return Code;
        }
    }
}
