﻿using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;

namespace Wabbajack.Lib.CompilationSteps
{
    class IncludeTaggedMods : ACompilationStep
    {
        private readonly IEnumerable<string> _includeDirectly;
        private string _tag;


        public IncludeTaggedMods(Compiler compiler, string tag) : base(compiler)
        {
            _tag = tag;
            _includeDirectly = _compiler.ModInis.Where(kv =>
            {
                var general = kv.Value.General;
                if (general.notes != null && general.notes.Contains(_tag))
                    return true;
                return general.comments != null && general.comments.Contains(_tag);
            }).Select(kv => $"mods\\{kv.Key}\\");
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith("mods")) return null;
            foreach (var modpath in _includeDirectly)
            {
                if (!source.Path.StartsWith(modpath)) continue;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
                return result;
            }

            return null;
        }
    }
}
