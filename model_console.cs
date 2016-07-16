using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

using CCon;
using static CCon.Model;
using static CCon.Utils;


class CLI {
    static void Main(string[] args) {
        Model model = Model.Load(args[0]);
        PyREPL("model", model);
    }

}
