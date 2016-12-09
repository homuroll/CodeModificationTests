using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using GrEmit.Injection;
using GrEmit.MethodBodyParsing;

using ManagedInjector;

using MethodBody = GrEmit.MethodBodyParsing.MethodBody;
using OpCodes = GrEmit.MethodBodyParsing.OpCodes;

namespace Injection
{
    class Program
    {
        private static Delegate del;

        static void Main(string[] args)
        {
            const string victim = "devenv";
            var process = Process.GetProcessesByName(victim)[0];
            Injector.Launch(process.MainWindowHandle, typeof(Program).Assembly.Location, "Injection.Program", "HookDevenv");
        }

        public static void HookDevenv()
        {
            var method = typeof(FileStream).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
            var body = MethodBody.Read(method, true);
            body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldstr, "TRACING: "));
            body.Instructions.Insert(1, Instruction.Create(OpCodes.Ldarg_1));
            var stringConcatMethod = typeof(string).GetMethod("Concat", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
            body.Instructions.Insert(2, Instruction.Create(OpCodes.Call, stringConcatMethod));
            var debugWriteLineMethod = typeof(Debug).GetMethod("WriteLine", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
            body.Instructions.Insert(3, Instruction.Create(OpCodes.Call, debugWriteLineMethod));

            var parameterTypes = new[] { method.DeclaringType }.Concat(method.GetParameters().Select(p => p.ParameterType)).ToArray();
            del = body.CreateDelegate(method.ReturnType, parameterTypes);
            Action unhook;
            if (!MethodUtil.HookMethod(method, del.Method, out unhook))
                Debug.WriteLine("Unable to hook method");
        }

        public static void HookMinesweeper()
        {
            var assembly = GetAssembly("Minesweeper");
            var form1 = GetType(assembly, "Minesweeper.Form1");

            var mouseDownMethod = GetPrivateMethod(form1, "Form1_MouseDown");
            var mouseMoveMethod = GetPrivateMethod(form1, "Form1_MouseMove");

            var gameStartedField = GetPrivateField(form1, "gameStarted");
            var fieldField = GetPrivateField(form1, "field");
            var getMethod = GetPublicMethod(fieldField.FieldType, "Get");
            var cellType = GetType(assembly, "Minesweeper.Cell");

            var cellTypeProp = GetPublicProperty(cellType, "Type");
            var cellTypeGetter = cellTypeProp.GetGetMethod();
            var cellHasMineProp = GetPublicProperty(cellType, "HasMine");
            var cellHasMineGetter = cellHasMineProp.GetGetMethod();

            var flagMineMethod = GetPrivateMethod(form1, "FlagMine");
            var openNeighboursMethod = GetPrivateMethod(form1, "OpenNeighbours");

            var mouseMoveBody = MethodBody.Read(mouseMoveMethod, true);
            var instructions = mouseMoveBody.Instructions;
            instructions.Clear();

            mouseMoveBody.AddLocalVariable(typeof(int));
            mouseMoveBody.AddLocalVariable(typeof(int));
            var lastInstr = Instruction.Create(OpCodes.Nop);
            var mouseDownBody = MethodBody.Read(mouseDownMethod, true);
            for (int i = 0; i < 44; ++i)
            {
                var instruction = mouseDownBody.Instructions[i];
                if (i == 42)
                    instruction.Operand = lastInstr;
                instructions.Add(instruction);
            }
            instructions.Add(lastInstr);


            mouseMoveBody.AddLocalVariable(cellType);

            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldfld, gameStartedField));
            var gameStartedInstr = Instruction.Create(OpCodes.Nop);
            instructions.Add(Instruction.Create(OpCodes.Brtrue, gameStartedInstr));
            instructions.Add(Instruction.Create(OpCodes.Ret));
            instructions.Add(gameStartedInstr);

            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldfld, fieldField));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_1));
            instructions.Add(Instruction.Create(OpCodes.Callvirt, getMethod));
            instructions.Add(Instruction.Create(OpCodes.Stloc_2));

            instructions.Add(Instruction.Create(OpCodes.Ldloc_2));
            instructions.Add(Instruction.Create(OpCodes.Call, cellTypeGetter));
            var cellUnopenedInstr = Instruction.Create(OpCodes.Nop);
            instructions.Add(Instruction.Create(OpCodes.Brfalse, cellUnopenedInstr));
            instructions.Add(Instruction.Create(OpCodes.Ret));
            instructions.Add(cellUnopenedInstr);

            var hasMineInstr = Instruction.Create(OpCodes.Nop);
            instructions.Add(Instruction.Create(OpCodes.Ldloc_2));
            instructions.Add(Instruction.Create(OpCodes.Call, cellHasMineGetter));
            instructions.Add(Instruction.Create(OpCodes.Brtrue, hasMineInstr));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_1));
            instructions.Add(Instruction.Create(OpCodes.Call, openNeighboursMethod));
            instructions.Add(Instruction.Create(OpCodes.Ret));
            instructions.Add(hasMineInstr);
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_1));
            instructions.Add(Instruction.Create(OpCodes.Call, flagMineMethod));
            instructions.Add(Instruction.Create(OpCodes.Ret));

            var parameterTypes = new[] { mouseMoveMethod.DeclaringType }.Concat(mouseMoveMethod.GetParameters().Select(p => p.ParameterType)).ToArray();
            del = mouseMoveBody.CreateDelegate(mouseMoveMethod.ReturnType, parameterTypes);

            Action unhook;
            if (!MethodUtil.HookMethod(mouseMoveMethod, del.Method, out unhook))
                Debug.WriteLine("Unable to hook MouseMove");
        }


        private static Assembly GetAssembly(string name)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith(name));
            if (assembly != null) return assembly;
            Debug.WriteLine(string.Format("Assembly '{0}' is not found", name));
            throw new InvalidOperationException();
        }

        private static Type GetType(Assembly assembly, string fullName)
        {
            var type = assembly.GetType(fullName);
            if (type != null) return type;
            Debug.WriteLine(string.Format("Type '{0}' is not found", fullName));
            throw new InvalidOperationException();
        }

        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null) return method;
            Debug.WriteLine(string.Format("Private method '{0}.{1}' is not found", type.FullName, name));
            throw new InvalidOperationException();
        }

        private static MethodInfo GetPublicMethod(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            if (method != null) return method;
            Debug.WriteLine(string.Format("Public method '{0}.{1}' is not found", type.FullName, name));
            throw new InvalidOperationException();
        }

        private static FieldInfo GetPrivateField(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return field;
            Debug.WriteLine(string.Format("Private field '{0}.{1}' is not found", type.FullName, name));
            throw new InvalidOperationException();
        }

        private static PropertyInfo GetPublicProperty(Type type, string name)
        {
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null) return property;
            Debug.WriteLine(string.Format("Public property '{0}.{1}' is not found", type.FullName, name));
            throw new InvalidOperationException();
        }

        public static void Hook()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("JetBrains.ReSharper.NewRefactorings"));
            if(assembly == null)
            {
                Debug.WriteLine("Assembly 'JetBrains.ReSharper.NewRefactorings' is not found");
                return;
            }
            var type = assembly.GetType("JetBrains.ReSharper.Refactorings.Rename.Impl.InlineRenameWorkflow");
            if(type == null)
            {
                Debug.WriteLine("Type 'JetBrains.ReSharper.Refactorings.Rename.Impl.InlineRenameWorkflow' is not found");
                return;
            }
            var preExecute = type.GetMethod("PreExecute", BindingFlags.Instance | BindingFlags.Public);
            if(preExecute == null)
            {
                Debug.WriteLine("Method 'JetBrains.ReSharper.Refactorings.Rename.Impl.InlineRenameWorkflow.PreExecute' is not found");
                return;
            }
            var body = MethodBody.Read(preExecute, true);
            //body.Instructions.Insert(0, Instruction.Create(OpCodes.Ldstr, "ZZZ"));
            //var debugWriteLineMethod = typeof(Debug).GetMethod("WriteLine", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);
            //body.Instructions.Insert(1, Instruction.Create(OpCodes.Call, debugWriteLineMethod));
            var startInstr = body.Instructions.FirstOrDefault(instr => instr.OpCode == OpCodes.Ldstr && (string)instr.Operand == "name");
            if (startInstr == null)
            {
                Debug.WriteLine("Instruction ldstr 'name' is not found");
                return;
            }
            var startInstrIndex = body.Instructions.IndexOf(startInstr);
            var nextInstr = body.Instructions[startInstrIndex + 1];
            if(nextInstr.OpCode != OpCodes.Ldloc_S)
            {
                Debug.WriteLine("Instruction ldloc.s is not found");
                return;
            }
            var locIndex = (int)nextInstr.Operand;
            var zassembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("JetBrains.ReSharper.Feature.Services"));
            if (zassembly == null)
            {
                Debug.WriteLine("Assembly 'JetBrains.ReSharper.Feature.Services' is not found");
                return;
            }
            var NameSuggestionsExpression_t = zassembly.GetType("JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.NameSuggestionsExpression");
            if (NameSuggestionsExpression_t == null)
            {
                Debug.WriteLine("Type 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.NameSuggestionsExpression' is not found");
                return;
            }
            var constructor = NameSuggestionsExpression_t.GetConstructor(new [] {typeof(ICollection<string>)});
            if (constructor == null)
            {
                Debug.WriteLine("Method 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.NameSuggestionsExpression.ctor' is not found");
                return;
            }
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Ldc_I4_1));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Newarr, typeof(string)));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Dup));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Ldc_I4_0));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Ldstr, "zzz"));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Stelem_Ref));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Newobj, constructor));
            body.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Stloc_S, locIndex));
            var parameterTypes = new[] { preExecute.DeclaringType }.Concat(preExecute.GetParameters().Select(p => p.ParameterType)).ToArray();
            del = body.CreateDelegate(preExecute.ReturnType, parameterTypes);

            Action unhook;
            if (!MethodUtil.HookMethod(preExecute, del.Method, out unhook))
                Debug.WriteLine("Unable to hook PreExecute");

            //var createAndExecute = type.GetMethod("CreateAndExecuteHotspotSession", BindingFlags.Instance | BindingFlags.NonPublic);
            //if (createAndExecute == null)
            //{
            //    Debug.WriteLine("Method 'JetBrains.ReSharper.Refactorings.Rename.Impl.InlineRenameWorkflow.CreateAndExecuteHotspotSession' is not found");
            //    return;
            //}
            //var body2 = MethodBody.Read(createAndExecute, true);
            //var hotspotSession = zassembly.GetType("JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.HotspotSession");
            //if (hotspotSession == null)
            //{
            //    Debug.WriteLine("Type 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.HotspotSession' is not found");
            //    return;
            //}
            //var execute = hotspotSession.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public);
            //if (execute == null)
            //{
            //    Debug.WriteLine("Method 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.HotspotSession.Execute' is not found");
            //    return;
            //}
            //var startSession = hotspotSession.GetMethod("StartSession", BindingFlags.Instance | BindingFlags.NonPublic);
            //if (startSession == null)
            //{
            //    Debug.WriteLine("Method 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.HotspotSession.StartSession' is not found");
            //    return;
            //}

            //var endSession = hotspotSession.GetMethod("EndSession", BindingFlags.Instance | BindingFlags.Public);
            //if (endSession == null)
            //{
            //    Debug.WriteLine("Method 'JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots.HotspotSession.EndSession' is not found");
            //    return;
            //}

            //var zinstr = body2.Instructions.FirstOrDefault(instr => instr.OpCode == OpCodes.Callvirt && (MethodInfo)instr.Operand == execute);
            //if(zinstr == null)
            //{
            //    Debug.WriteLine("Instruction callvirt HotspotSession.Execute is not found");
            //    return;
            //}
            //startInstrIndex = body2.Instructions.IndexOf(zinstr) - 1;
            ////body2.Instructions[startInstrIndex] = Instruction.Create(OpCodes.Dup);
            //body2.Instructions[++startInstrIndex] = Instruction.Create(OpCodes.Call, startSession);
            //body2.Instructions[++startInstrIndex] = Instruction.Create(OpCodes.Nop);
            ////body2.Instructions[++startInstrIndex] = Instruction.Create(OpCodes.Ldc_I4_2);
            ////body2.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Callvirt, endSession));

            ////body2.Instructions[startInstrIndex] = Instruction.Create(OpCodes.Pop);
            ////body2.Instructions[++startInstrIndex] = Instruction.Create(OpCodes.Nop);
            ////body2.Instructions[++startInstrIndex] = Instruction.Create(OpCodes.Nop);
            ////body2.Instructions.Insert(++startInstrIndex, Instruction.Create(OpCodes.Callvirt, endSession));

            ////var count = body2.Instructions.Count;
            ////for(int i = 0; i < count; ++i)
            ////    body2.Instructions.RemoveAt(0);
            ////body2.Instructions.Insert(0, Instruction.Create(OpCodes.Ret));
            ////body2.ExceptionHandlers.Clear();

            //body2.Seal();
            //Debug.WriteLine(body2);

            //var parameterTypes2 = new[] { createAndExecute.DeclaringType }.Concat(createAndExecute.GetParameters().Select(p => p.ParameterType)).ToArray();
            //del2 = body2.CreateDelegate(createAndExecute.ReturnType, parameterTypes2);

            //Action unhook2;
            //if(!MethodUtil.HookMethod(createAndExecute, del2.Method, out unhook2))
            //    Debug.WriteLine("Unable to hook CreateAndExecute");
        }
    }
}