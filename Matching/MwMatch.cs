using Microsoft.Scripting.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Matching
{
    public class MwMatch
    {
        private readonly ScriptScope _scope;

        public MwMatch()
        {
            Microsoft.Scripting.Hosting.ScriptEngine pythonEngine = IronPython.Hosting.Python.CreateEngine();

            ICollection<string> searchPaths = pythonEngine.GetSearchPaths();
            searchPaths.Add("..\\..");
            searchPaths.Add(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));

            pythonEngine.SetSearchPaths(searchPaths);

            _scope = IronPython.Hosting.Python.ImportModule(pythonEngine, "mwmatching");
        }

        public int[] MaxWeightMatching(int[][] edges)
        {
            dynamic maxWeightMatching = _scope.GetVariable("maxWeightMatching");
            IronPython.Runtime.List ret = maxWeightMatching(edges, true);
            return ret.ToArray().Cast<int>().ToArray();
        }
    }
}
