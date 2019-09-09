﻿using Blueprint.Compiler.Model;

namespace Blueprint.Compiler.Frames
{
    public class IfBlock : CompositeFrame
    {
        public string Condition { get; }

        public IfBlock(string condition, params Frame[] inner) : base(inner)
        {
            Condition = condition;
        }

        public IfBlock(Variable variable, params Frame[] inner) : this(variable.Usage, inner)
        {

        }

        protected override void GenerateCode(GeneratedMethod method, ISourceWriter writer, Frame inner)
        {
            writer.Write($"BLOCK:if ({Condition})");
            inner.GenerateCode(method, writer);
            writer.FinishBlock();
        }
    }
}
