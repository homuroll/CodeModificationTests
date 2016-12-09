using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using GrEmit.MethodBodyParsing;
using GrEmit.Utils;

using RGiesecke.DllExport;

using MethodBody = GrEmit.MethodBodyParsing.MethodBody;

namespace ManagedTrace
{
    public static unsafe class TraceInstaller
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate uint SignatureTokenBuilderDelegate(UIntPtr moduleId, byte* signature, int len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate byte* MethodBodyAllocator(UIntPtr moduleId, uint size);

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        public static void Init([MarshalAs(UnmanagedType.FunctionPtr)] SignatureTokenBuilderDelegate signatureTokenBuilderDelegate)
        {
            signatureTokenBuilder = (moduleId, signature) =>
                {
                    fixed(byte* b = &signature[0])
                    {
                        var token = signatureTokenBuilderDelegate(moduleId, b, signature.Length);
                        return new MetadataToken(token);
                    }
                };

            var debugOutputMethod = HackHelpers.GetMethodDefinition<int>(x => DebugOutput(x));
            RuntimeHelpers.PrepareMethod(debugOutputMethod.MethodHandle);
            debugOutputAddress = debugOutputMethod.MethodHandle.GetFunctionPointer();
            debugOutputSignature = debugOutputMethod.Module.ResolveSignature(debugOutputMethod.MetadataToken);
        }

        private static void DebugOutput(int id)
        {
            Debug.WriteLine(methods[id]);
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        public static byte* InstallTracing(
            [MarshalAs(UnmanagedType.LPWStr)] string assemblyName,
            [MarshalAs(UnmanagedType.LPWStr)] string moduleName,
            UIntPtr moduleId,
            uint methodToken,
            byte* rawMethodBody,
            [MarshalAs(UnmanagedType.FunctionPtr)] MethodBodyAllocator allocateForMethodBody)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == assemblyName);

            var module = assembly.GetModules().First(m => !m.Assembly.IsDynamic && m.FullyQualifiedName == moduleName);

            var method = module.ResolveMethod((int)methodToken);

            if (method.IsConstructor)
                return null;

            var methodBody = MethodBody.Read(rawMethodBody, module, new MetadataToken(methodToken), false);

            if(methodBody.Instructions.Count < 20)
                return null;

            var curMethodId = Interlocked.Increment(ref methodId) - 1;
            methods[curMethodId] = $"TRACING: {Format(method)}";

            methodBody.Instructions.Insert(0, Instruction.Create(OpCodes.Ldc_I4, curMethodId));
            if(IntPtr.Size == 4)
                methodBody.Instructions.Insert(1, Instruction.Create(OpCodes.Ldc_I4, debugOutputAddress.ToInt32()));
            else
                methodBody.Instructions.Insert(1, Instruction.Create(OpCodes.Ldc_I8, debugOutputAddress.ToInt64()));
            methodBody.Instructions.Insert(2, Instruction.Create(OpCodes.Calli, signatureTokenBuilder(moduleId, debugOutputSignature)));

            var methodBytes = methodBody.GetFullMethodBody(sig => signatureTokenBuilder(moduleId, sig), Math.Max(methodBody.MaxStack, 2));

            var newMethodBody = allocateForMethodBody(moduleId, (uint)methodBytes.Length);
            Marshal.Copy(methodBytes, 0, (IntPtr)newMethodBody, methodBytes.Length);

            return newMethodBody;
        }

        private static string Format(MethodBase method)
        {
            var methodInfo = method as MethodInfo;
            return methodInfo != null ? Formatter.Format(methodInfo) : method.ToString();
        }

        private static Func<UIntPtr, byte[], MetadataToken> signatureTokenBuilder;

        private static int methodId;
        private static readonly string[] methods = new string[1000000];
        private static IntPtr debugOutputAddress;
        private static byte[] debugOutputSignature;
    }
}