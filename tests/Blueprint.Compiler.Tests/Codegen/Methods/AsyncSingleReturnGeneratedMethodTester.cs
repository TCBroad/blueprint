﻿using System.Threading.Tasks;
using Blueprint.Compiler.Frames;
using FluentAssertions;
using NUnit.Framework;

namespace Blueprint.Compiler.Tests.Codegen.Methods
{
    public class AsyncSingleReturnGeneratedMethodTester
    {
        [Test]
        public async Task can_generate_method()
        {
            var assembly = Builder.Assembly();

            var generatedType = assembly.AddType("NumberGetter", typeof(INumberGetter));

            generatedType.MethodFor("GetNumber").Frames.Append(new ReturnFive());

            assembly.CompileAll();

            var getter = generatedType.CreateInstance<INumberGetter>();

            var number = await getter.GetNumber();

            number.Should().Be(5);
        }
    }

    public class ReturnFive : AsyncFrame
    {
        public override bool CanReturnTask()
        {
            return true;
        }

        public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
        {
            writer.Write("return Task.FromResult(5);");
        }
    }

    public interface INumberGetter
    {
        Task<int> GetNumber();
    }
}
