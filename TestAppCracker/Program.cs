using System.IO;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TestAppCracker
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var assemblyResolver = new DefaultAssemblyResolver();
            assemblyResolver.AddSearchDirectory(@"c:\Windows\Microsoft.NET\Framework64\v4.0.30319");
            var readerParameters = new ReaderParameters(ReadingMode.Deferred)
                {
                    AssemblyResolver = assemblyResolver
                };
            const string binaryName = "minesweeper.exe";
            var assembly = AssemblyDefinition.ReadAssembly(binaryName, readerParameters);

            var productLicense = assembly.MainModule.Types.First(type => type.FullName == "Minesweeper.ProductLicense");
            var trialDaysGetter = productLicense.Methods.First(method => method.Name == "get_TrialDays");
            var body = trialDaysGetter.Body;
            var instruction = body.Instructions.First(instr => instr.OpCode == OpCodes.Ldc_I4_S && (sbyte)instr.Operand == 30);
            //var productLicense = assembly.MainModule.Types.First(type => type.FullName == "JetBrains.dotMemory.Core.shell.DotMemoryLicensedEntity");
            //var trialDaysGetter = productLicense.Methods.First(method => method.Name == "get_FreeTrialPeriod");
            //var body = trialDaysGetter.Body;
            //var instruction = body.Instructions.First(instr => instr.OpCode == OpCodes.Ldc_I4_5);
            instruction.OpCode = OpCodes.Ldc_I4;
            instruction.Operand = 1000000000;

            assembly.Write(binaryName);
        }
    }
}