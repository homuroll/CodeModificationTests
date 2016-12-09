using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using GrEmit.Injection;
using GrEmit.MethodBodyParsing;
using GrEmit.Utils;

using NUnit.Framework;

using OpCodes = GrEmit.MethodBodyParsing.OpCodes;
using MethodBody = GrEmit.MethodBodyParsing.MethodBody;

namespace Mocks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var method = typeof(FileStream).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
            var body = MethodBody.Read(method, true);
            body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldarg_1));
            var debugWriteLineMethod = typeof(Debug).GetMethod("WriteLine", BindingFlags.Static | BindingFlags.Public, null, new[] {typeof(string)}, null);
            body.Instructions.Insert(1, Instruction.Create(OpCodes.Call, debugWriteLineMethod));
            Console.WriteLine(body);

            var parameterTypes = new [] {method.DeclaringType}.Concat(method.GetParameters().Select(p => p.ParameterType)).ToArray();
            var del = body.CreateDelegate(method.ReturnType, parameterTypes);
            Action unhook;
            if(!MethodUtil.HookMethod(method, del.Method, out unhook))
                throw new InvalidOperationException("Unable to hook method");
            File.WriteAllText(@"c:\temp\test.txt", "test");

            using (var mock = new Mock<int, int[]>(x => DataReader.Read(x)))
            {
                //mock.Set(MockedMethod);
                mock.Set(x =>
                    {
                        if (x == 42) return new[] { 1, 4, 7, 8 };
                        throw new InvalidOperationException();
                    });
                var dataProcessor = new DataProcessor();
                Assert.AreEqual(5, dataProcessor.FindAverage(42));
            }
        }

        private static int[] MockedMethod(int x)
        {
            if(x == 42) return new[] {1, 4, 7, 8};
            throw new InvalidOperationException();
        }
    }

    public class Mock<T, TResult> : IDisposable
    {
        public Mock(Expression<Func<T, TResult>> exp)
        {
            victim = ((MethodCallExpression)exp.Body).Method;
            RuntimeHelpers.PrepareMethod(victim.MethodHandle);
        }

        public void Set(Func<T, TResult> func)
        {
            if(victim.IsStatic && !func.Method.IsStatic)
                func = Clone(func);
            curFunc = func;

            Action curUnhook;
            if(!MethodUtil.HookMethod(victim, func.Method, out curUnhook))
                throw new InvalidOperationException(String.Format("Unable to hook method '{0}'", Formatter.Format(victim)));
            if(unhook == null)
                unhook = curUnhook;
        }

        public void Dispose()
        {
            if(unhook != null)
                unhook();
        }

        private static Func<T, TResult> Clone(Func<T, TResult> func)
        {
            var body = MethodBody.Read(func.Method, true);
            foreach(var instruction in body.Instructions)
            {
                switch(instruction.OpCode.Code)
                {
                    case Code.Ldarg_0:
                        throw new InvalidOperationException(String.Format("Method '{0}' has different signature", Formatter.Format(func.Method)));
                    case Code.Ldarg_1:
                        instruction.OpCode = OpCodes.Ldarg_0;
                    break;
                    case Code.Ldarg_2:
                        instruction.OpCode = OpCodes.Ldarg_1;
                    break;
                }
            }

            return  body.CreateDelegate<Func<T, TResult>>();
        }

        private readonly MethodInfo victim;
        private Action unhook;
        private Func<T, TResult> curFunc;
    }

    public class DataProcessor
    {
        public int FindAverage(int column)
        {
            var data = DataReader.Read(column);
            var sum = data.Sum(x => (long)x);
            return (int)(sum / data.Length);
        }
    }

    public static class DataReader
    {
        public static int[] Read(int column)
        {
            var data = File.ReadAllLines("data.txt");
            var result = new int[data.Length];
            for(int i = 0; i < data.Length; ++i)
                result[i] = int.Parse(data[i].Split(',')[column]);
            return result;
        }
    }
}