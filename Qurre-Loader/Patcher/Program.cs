using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Patcher
{
    internal class Program
    {
        public static void Main(string[] _)
        {
            Console.WriteLine("Write path of file");
            Inject(Console.ReadLine().ToLower());

            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }

        static void Inject(string com)
        {
            var module = ModuleDefMD.Load(com);
            if (module is null)
            {
                Console.WriteLine("Assembly file not found");
                return;
            }

            module.IsILOnly = true;
            module.VTableFixups = null;
            module.Assembly.PublicKey = null;
            module.Assembly.HasPublicKey = false;

            Console.WriteLine($"Loaded {module.Name}");
            Console.WriteLine("Assembly: Resolving References..");

            module.Context = ModuleDef.CreateModuleContext();
            ((AssemblyResolver)module.Context.AssemblyResolver).AddToCache(module);

            Console.WriteLine("Injection of Loader");

            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Loader.dll")))
            {
                Console.WriteLine("Loader file not found");
                return;
            }

            var loader = ModuleDefMD.Load("Loader.dll");

            Console.WriteLine($"Injector: Loaded {loader.Name}");

            TypeDef modRefType = default;
            TypeDef initType = default;

            List<TypeDef> _list = new();
            foreach (var type in loader.Types)
                _list.Add(type);

            foreach (var type in _list)
            {
                if (type.Name == "<Module>")
                    continue;

                loader.Types.Remove(type);

                type.DeclaringType = null;
                module.Types.Add(type);

                Console.WriteLine($"Injecting: {type.Namespace}.{type.Name}");

                if (type.Name == "Loader")
                {
                    modRefType = type;
                }
                else if (type.Name == "MainInitializator")
                {
                    initType = type;
                }
            }

            foreach (var n in module.Assembly.Modules.SelectMany(t => t.Types).Where(x => x.Name == "RoundSummary")
                .SelectMany(t => t.NestedTypes))
            {
                if (n.Name == "LeadingTeam")
                {
                    n.Attributes = TypeAttributes.Public;
                }
            }

            var call = FindMethod(modRefType, "LoadModSystem");

            if (call is null)
            {
                Console.WriteLine("Failed to get 'LoadModSystem'");
                return;
            }

            var callReal = FindMethod(initType, "Init");

            if (callReal is null)
            {
                Console.WriteLine("Failed to get 'Init'");
                return;
            }

            Console.WriteLine("Injected");
            Console.WriteLine("Patching...");

            var defRem = FindType(module.Assembly, "AdminToys.AdminToyBase");
            MethodDef bctorRem = FindMethod(defRem, "LateUpdate");
            defRem.Methods.Remove(bctorRem);

            var def = FindType(module.Assembly, "ServerConsole");

            MethodDef bctor = FindMethod(def, "Start");

            if (bctor is null)
            {
                bctor = new MethodDefUser("Start", MethodSig.CreateInstance(module.CorLibTypes.Void),
                   MethodImplAttributes.IL | MethodImplAttributes.Managed,
                   MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                def.Methods.Add(bctor);
            }

            bctor.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(call));
            bctor.Body.Instructions.Insert(bctor.Body.Instructions.Count() - 1, OpCodes.Call.ToInstruction(callReal));

            module.Write("Assembly-CSharp.dll");

            Console.WriteLine("Patched");

            Console.WriteLine("Creating Publicized DLL");

            var expTypes = module.Assembly.Modules.SelectMany(t => t.ExportedTypes);
            var allTypes = module.Assembly.Modules.SelectMany(t => t.Types);
            var allMethods = allTypes.SelectMany(t => t.Methods).ToList();
            var allFields = allTypes.SelectMany(t => t.Fields).ToList();

            var allNt = allTypes.SelectMany(t => t.NestedTypes);
            allMethods.AddRange(allNt.SelectMany(t => t.Methods));
            allFields.AddRange(allNt.SelectMany(t => t.Fields));

            foreach (var exprt in expTypes)
            {
                if (exprt is null)
                    continue;

                if (exprt.IsPublic)
                    continue;

                exprt.Attributes = exprt.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public;
            }

            foreach (var type in allTypes)
            {
                if (type is null)
                    continue;

                if (type.IsPublic)
                    continue;

                type.Attributes = type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public;
            }

            foreach (var method in allMethods)
            {
                if (method is null)
                    continue;

                if (method.IsPublic)
                    continue;

                method.Access = MethodAttributes.Public;
            }

            foreach (var field in allFields)
            {
                if (field is null)
                    continue;

                if (field.IsPublic)
                    continue;

                if (field.Name == "OnMapGenerated")
                    continue;

                if (field.Name == "ServerOnSettingValueReceived")
                    continue;

                field.Access = FieldAttributes.Public;
            }


            foreach (var type in allNt)
            {
                if (type is null)
                    continue;

                if (type.IsPublic)
                    continue;

                if (!type.IsNested)
                    continue;

                type.Attributes &= ~TypeAttributes.NestedPrivate;
                type.Attributes |= TypeAttributes.NestedPublic;
            }

            {
                var type1 = FindType(module.Assembly, "MapGeneration.Distributors.Scp079Generator");
                if (type1 is not null)
                {
                    var array = type1.NestedTypes.Where(x => x.Name == "GeneratorFlags");
                    if (array.Any())
                    {
                        array.First().Attributes = TypeAttributes.NestedPublic;
                    }
                }
            }

            module.Write("Assembly-CSharp_public.dll");

            Console.WriteLine("Created Publicized DLL");
        }

        static MethodDef FindMethod(TypeDef type, string methodName)
        {
            return type?.Methods.FirstOrDefault(method => method.Name == methodName);
        }

        static TypeDef FindType(AssemblyDef asm, string classPath)
        {
            return asm.Modules.SelectMany(module => module.Types).FirstOrDefault(type => type.FullName == classPath);
        }
    }
}