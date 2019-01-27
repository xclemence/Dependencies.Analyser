using System;
using System.Collections.Generic;
using System.Text;

namespace Dependencies.Analyser.Base
{
    public interface IAssemblyAnalyserFactory
    {
        string Name { get; }

        string Code { get; }

        IAssemblyAnalyser GetAnalyser();
    }
}
