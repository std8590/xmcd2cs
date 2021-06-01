using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace xmcd2cs
{
    public class Parser
    {
        private static readonly XNamespace ml = "http://schemas.mathsoft.com/math30";
        private static readonly XNamespace ws = "http://schemas.mathsoft.com/worksheet30";
        private static readonly XNamespace u = "http://schemas.mathsoft.com/units10";
        public HashSet<string> ToImplement { get; set; }
        public Parser()
        {
            ToImplement = new HashSet<string>();
        }
        public string Parse(string file)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using System.Collections.Generic;");
            stringBuilder.AppendLine("using System.Linq;");
            stringBuilder.AppendLine("using System.Text;");
            stringBuilder.AppendLine("using System.Threading.Tasks;");
            stringBuilder.AppendLine("using MathNet.Numerics.LinearAlgebra;");
            stringBuilder.AppendLine("");
            stringBuilder.AppendLine("namespace Mathcad2Cs");
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine($"    public class {Path.GetFileNameWithoutExtension(file)}");
            stringBuilder.AppendLine("    {");


            var root = XElement.Load(file);
            TraverseElements(root.Elements(), stringBuilder);

            stringBuilder.AppendLine("    }");
            stringBuilder.AppendLine("}");
            return stringBuilder.ToString();
        }

        private void TraverseElements(IEnumerable<XElement> childs, StringBuilder stringBuilder)
        {
            foreach (var child in childs)
            {
                var math = false;
                if (child.Parent.Name == ws + "math")
                {
                    math = true;
                }
                if (child.Name.Namespace == ml)
                {
                    if (child.Name == ml + "define")
                    {
                        ParseDefinition(child, stringBuilder);
                    }
                    else if (child.Name == ml + "eval")
                    {
                        ParseEval(child, stringBuilder);
                    }
                    else if (child.Name == ml + "globalDefine")
                    {
                        ParseDefinition(child, stringBuilder);
                    }
                    else if (math && child.Name == ml + "apply")
                    {
                        var apply = ParseApply(child);
                        stringBuilder.AppendLine($"public double Function {{ get {{ return {apply}; }} }}");
                    }
                    else
                    {
                        ToImplement.Add(child.Name.LocalName);
                    }
                }
                TraverseElements(child.Elements(), stringBuilder);
            }
        }

        private void ParseDefinition(XElement define, StringBuilder stringBuilder)
        {
            var elements = define.Elements().ToList();
            if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "real")
            {
                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = {GetValue(elements[1])};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "id")
            {
                stringBuilder.AppendLine($"double {GetValue(elements[0])} = {GetValue(elements[1])};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "apply")
            {
                var apply = ParseApply(elements[1]);

                stringBuilder.AppendLine($"public double {GetValue(elements[0])} {{ get {{ return {apply}; }} }}");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "function" && elements[1].Name == ml + "apply")
            {
                var functionElement = ParseFunction(elements[0]);
                stringBuilder.AppendLine(functionElement);
                stringBuilder.AppendLine("{");
                var apply = ParseApply(elements[1]);
                stringBuilder.AppendLine($"return {apply};");
                stringBuilder.AppendLine("}");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "function" && elements[1].Name == ml + "parens")
            {
                var functionElement = ParseFunction(elements[0]);
                stringBuilder.AppendLine(functionElement);
                stringBuilder.AppendLine("{");
                var parens = ParseParens(elements[1]);
                stringBuilder.AppendLine($"return {parens};");
                stringBuilder.AppendLine("}");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "range")
            {
                var range = ParseRange(elements[1]);

                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = {range};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "matrix")
            {
                var matrix = ParseMatrix(elements[1]);

                stringBuilder.AppendLine($"public var {GetValue(elements[0])} = {matrix};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "program")
            {
                var program = ParseProgram(elements[1]);

                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = {program};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "parens")
            {
                stringBuilder.AppendLine($"public double {GetValue(elements[0])} {{ get {{ return {ParseParens(elements[1])}; }} }}");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "apply")
            {
                var apply1 = ParseApply(elements[0]);
                var apply2 = ParseApply(elements[1]);
                stringBuilder.AppendLine($"{apply1}={apply2};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "parens")
            {
                var apply = ParseApply(elements[0]);
                var parens = ParseParens(elements[1]);
                stringBuilder.AppendLine($"{apply}={parens};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "program")
            {
                var apply = ParseApply(elements[0]);
                var program = ParseProgram(elements[1]);
                stringBuilder.AppendLine($"{apply}={program};");
            }
            else
            {
                Debug.Fail("Unknown define");
            }
        }

        private string ParseApply(XElement apply)
        {
            var elements = apply.Elements().ToList();

            if (elements.Count == 3)
            {
                if (elements[0].Name == ml + "mult" ||
                    elements[0].Name == ml + "minus" ||
                    elements[0].Name == ml + "div" ||
                    elements[0].Name == ml + "plus" ||
                    elements[0].Name == ml + "lessThan" ||
                    elements[0].Name == ml + "greaterThan" ||
                    elements[0].Name == ml + "greaterOrEqual" ||
                    elements[0].Name == ml + "lessOrEqual" ||
                    elements[0].Name == ml + "equal")
                {
                    string operatr = string.Empty;
                    if (elements[0].Name == ml + "mult")
                    {
                        operatr = "*";
                    }
                    else if (elements[0].Name == ml + "minus")
                    {
                        operatr = "-";
                    }
                    else if (elements[0].Name == ml + "div")
                    {
                        operatr = "/";
                    }
                    else if (elements[0].Name == ml + "plus")
                    {
                        operatr = "+";
                    }
                    else if (elements[0].Name == ml + "lessThan")
                    {
                        operatr = "<";
                    }
                    else if (elements[0].Name == ml + "greaterThan")
                    {
                        operatr = ">";
                    }
                    else if (elements[0].Name == ml + "greaterOrEqual")
                    {
                        operatr = ">=";
                    }
                    else if (elements[0].Name == ml + "lessOrEqual")
                    {
                        operatr = "<=";
                    }
                    else if (elements[0].Name == ml + "equal")
                    {
                        operatr = "==";
                    }
                    else
                    {
                        Debug.Fail("Unknown operator!");
                    }

                    string firstValue = GetValue(elements[1]);
                    string secondValue = GetValue(elements[2]);

                    if (elements[0].Name == ml + "plus" && elements[1].Name == ml + "apply" && elements[2].Name == ml + "apply") //possible complex
                    {
                        var imag1 = elements[1].Descendants(ml + "imag").ToList();
                        var imag2 = elements[2].Descendants(ml + "imag").ToList();
                        if (imag2.Count > 0 && imag1.Count == 0) // imag1 = real
                        {
                            return $"new Complex({firstValue}, {secondValue})";
                        }

                        if (imag1.Count > 0 && imag2.Count == 0) // imag2 = real
                        {
                            return $"new Complex({secondValue}, {firstValue})";
                        }
                    }
                    return $"({firstValue} {operatr} {secondValue})";
                }
                else if (elements[0].Name == ml + "nthRoot")
                {
                    string firstValue = GetValue(elements[1]);
                    string secondValue = GetValue(elements[2]);
                    return $"(Math.Pow({secondValue}, 1.0 / {firstValue}))";
                }
                else if (elements[0].Name == ml + "pow")
                {
                    string firstValue = GetValue(elements[1]);
                    string secondValue = GetValue(elements[2]);
                    return $"(Math.Pow({firstValue}, {secondValue}))";
                }
                else if (elements[0].Name == ml + "indexer")
                {
                    string firstValue = GetValue(elements[1]);
                    string secondValue = GetValue(elements[2]);
                    return $"({firstValue}[{secondValue}])";
                }
                else
                {
                    Debug.Fail("Unknown apply three elements");
                }
            }
            else if (elements.Count == 2)
            {
                if (elements[0].Name == ml + "id" && elements[1].Name == ml + "id")
                {
                    string firstValue = GetValue(elements[0]);
                    string secondValue = GetValue(elements[1]);

                    if (firstValue == "Math.Log10" && secondValue == "e")
                    {
                        secondValue = "Math.E";
                    }
                    return $"({firstValue}({secondValue}))";
                }
                if (elements[0].Name == ml + "id" && elements[1].Name == ml + "real")
                {
                    string firstValue = GetValue(elements[0]);
                    string secondValue = GetValue(elements[1]);

                    return $"({firstValue}({secondValue}))";
                }
                else if (elements[0].Name == ml + "id" && elements[1].Name == ml + "apply")
                {
                    return $"({GetValue(elements[0])}({ParseApply(elements[1])}))";
                }
                else if (elements[0].Name == ml + "sqrt" && elements[1].Name == ml + "apply")
                {
                    var applyElement = elements[1];
                    return $"(Math.Sqrt({ParseApply(applyElement)}))";
                }
                else if (elements[0].Name == ml + "absval" && elements[1].Name == ml + "apply")
                {
                    var applyElement = elements[1];
                    return $"(Math.Abs({ParseApply(applyElement)}))";
                }
                else if (elements[0].Name == ml + "sqrt" && elements[1].Name == ml + "id")
                {
                    return $"(Math.Sqrt({GetValue(elements[1])}))";
                }
                else if (elements[0].Name == ml + "neg" && elements[1].Name == ml + "id")
                {
                    return $"(-1.0 * ({GetValue(elements[1])}))";
                }
                else if (elements[0].Name == ml + "neg" && elements[1].Name == ml + "apply")
                {
                    var applyElement = elements[1];
                    return $"(-1.0 * ({ParseApply(applyElement)}))";
                }
                else if (elements[0].Name == ml + "id" && elements[1].Name == ml + "sequence")
                {
                    var id = GetValue(elements[0]);
                    var sequence = ParseSequence(elements[1]);
                    if (id == "if")
                    {
                        // remove comma in pow
                        var index = sequence.IndexOf("Math.Pow");
                        while (index >= 0)
                        {
                            var commaIndex = sequence.IndexOf(",", index);
                            var bytes = sequence.ToCharArray();
                            bytes[commaIndex] = ';';
                            sequence = new string(bytes);
                            index = sequence.IndexOf("Math.Pow", index + 1);
                        }

                        // remove comma in []
                        index = sequence.IndexOf("[");
                        while (index >= 0)
                        {
                            var endIndex = sequence.IndexOf("]", index);
                            if (endIndex >= 0)
                            {
                                var commaIndex = sequence.IndexOf(",", index, endIndex - index);
                                if (commaIndex >= 0)
                                {
                                    var bytes = sequence.ToCharArray();
                                    bytes[commaIndex] = ';';
                                    sequence = new string(bytes);
                                }
                            }
                            index = sequence.IndexOf("[", index + 1);
                        }

                        var split = sequence.Split(',');
                        Debug.Assert(split.Length == 3, "correct if then else");
                        var res = $"({split[0]} ? ({split[1]}) : {split[2]})";

                        // add comma in pow
                        index = res.IndexOf("Math.Pow");
                        while (index >= 0)
                        {
                            var commaIndex = res.IndexOf(";", index);
                            var bytes = res.ToCharArray();
                            bytes[commaIndex] = ',';
                            res = new string(bytes);
                            index = res.IndexOf("Math.Pow", index + 1);
                        }

                        // add comma in []
                        index = res.IndexOf("[");
                        while (index >= 0)
                        {
                            var endIndex = res.IndexOf("]", index);
                            if (endIndex >= 0)
                            {
                                var commaIndex = res.IndexOf(";", index, endIndex - index);
                                if (commaIndex >= 0)
                                {
                                    var bytes = res.ToCharArray();
                                    bytes[commaIndex] = ',';
                                    res = new string(bytes);
                                }
                            }
                            index = res.IndexOf("[", index + 1);
                        }
                        return res;
                    }
                    return $"({id}({sequence}))";
                }
                else if (elements[0].Name == ml + "Find" && elements[1].Name == ml + "id")
                {
                    // https://numerics.mathdotnet.com/LinearEquations.html
                    return $"(Solve({GetValue(elements[1])}))";
                }
                else if (elements[0].Name == ml + "vectorSum" && elements[1].Name == ml + "id")
                {
                    // https://numerics.mathdotnet.com/LinearEquations.html
                    return $"(Vector.Sum({GetValue(elements[1])}))";
                }
                else
                {
                    Debug.Fail("Unknown apply two elements");
                }

            }
            else if (elements.Count == 0)
            {
                return string.Empty;
            }
            else
            {
                Debug.Fail("Unknown apply");
            }

            return string.Empty;
        }

        private string GetValue(XElement valueElement)
        {
            string val = string.Empty;
            if (valueElement.Name == ml + "real")
            {
                var realElement = valueElement;
                val = realElement.Value;
            }
            else if (valueElement.Name == ml + "id")
            {
                var idElement = valueElement;
                var subscript = string.Empty;
                var subscriptAttribute = idElement.Attribute("subscript");
                if (subscriptAttribute != null)
                {
                    subscript = $"_{subscriptAttribute.Value}";
                }
                val = $"{idElement.Value}{subscript}";
                if (val == "π")
                {
                    val = "Math.PI";
                }
                else if (val == "ln")
                {
                    val = "Math.Log";
                }
                else if (val == "log")
                {
                    val = "Math.Log10";
                }
            }
            else if (valueElement.Name == ml + "apply")
            {
                val = ParseApply(valueElement);
            }
            else if (valueElement.Name == ml + "imag")
            {
                val = $"{valueElement.Value}i";
            }
            else if (valueElement.Name == ml + "parens")
            {
                val = ParseParens(valueElement);
            }
            else if (valueElement.Name == ml + "sequence")
            {
                val = ParseSequence(valueElement);
            }
            else
            {
                Debug.Fail("Unknown value!");
            }

            return val;
        }

        private string ParseFunction(XElement function)
        {
            var elements = function.Elements().ToList();
            if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "boundVars")
            {
                var func = new StringBuilder();
                func.Append($"public double {GetValue(elements[0])}(");
                var boundVarsElement = elements[1];

                foreach (var v in boundVarsElement.Elements())
                {
                    func.Append($"double {GetValue(v)},");
                }
                func.Remove(func.Length - 1, 1);
                func.Append(")");
                return func.ToString();
            }
            else
            {
                Debug.Fail("Unknown function");
            }

            return string.Empty;
        }

        private string ParseRange(XElement range)
        {
            var elements = range.Elements().ToList();
            if (elements.Count == 2 && elements[0].Name == ml + "real" && elements[1].Name == ml + "parens")
            {
                var parens = ParseParens(elements[1]);
                return $"Enumerable.Range({GetValue(elements[0])}, {parens} - {GetValue(elements[0])})";
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "real" && elements[1].Name == ml + "id")
            {
                return $"Enumerable.Range({GetValue(elements[0])}, {GetValue(elements[1])})";
            }
            else
            {
                Debug.Fail("Unknown function");
            }

            return string.Empty;
        }

        private string ParseParens(XElement parens)
        {
            var elements = parens.Elements().ToList();
            if (elements.Count == 1 && elements[0].Name == ml + "apply")
            {
                return GetValue(elements[0]);
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "real")
            {
                return GetValue(elements[0]);
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "id")
            {
                return GetValue(elements[0]);
            }
            else
            {
                Debug.Fail("Unknown parens");
            }

            return string.Empty;
        }

        private void ParseEval(XElement eval, StringBuilder stringBuilder)
        {
            var elements = eval.Elements().ToList();
            if (elements.Count == 3 && elements[0].Name == ml + "id" && elements[1].Name == ml + "unitOverride" && elements[2].Name == ml + "result")
            {
                var unit = ParseUnitOverride(elements[1]);
                var val = ParseResult(elements[2]);

                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = {val} {unit};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "result")
            {
                var applyElement = elements[0];
                var apply = ParseApply(applyElement);
                var val = ParseResult(elements[1]);
                stringBuilder.AppendLine($"public double {apply} = {val};");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "id" && elements[1].Name == ml + "result")
            {
                var val = ParseResult(elements[1]);
                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = {val};");
            }
            else if (elements.Count == 3 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "unitOverride" && elements[2].Name == ml + "result")
            {
                var applyElement = elements[0];
                var apply = ParseApply(applyElement);
                var unit = ParseUnitOverride(elements[1]);
                var val = ParseResult(elements[2]);

                stringBuilder.AppendLine($"public double {apply} = {val} {unit};");
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "id")
            {
                stringBuilder.AppendLine($"public double {GetValue(elements[0])} = 0;");
            }
            else
            {
                Debug.Fail("Unknown eval");
            }
        }
        private string ParseUnitOverride(XElement unitOverride)
        {
            var elements = unitOverride.Elements().ToList();
            if (elements.Count == 1 && elements[0].Name == ml + "apply")
            {
                var applyElement = elements[0];
                return ParseApply(applyElement);
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "id")
            {
                return GetValue(elements[0]);
            }
            else
            {
                Debug.Fail("Unknown unit override");
            }

            return string.Empty;
        }

        private string ParseResult(XElement result)
        {
            var elements = result.Elements().ToList();
            if (elements.Count == 1 && elements[0].Name == ml + "unitedValue")
            {
                var unitedValueElement = elements[0];
                var unitedElements = unitedValueElement.Elements().ToList();
                if (unitedElements.Count == 2 && unitedElements[0].Name == ml + "real" && unitedElements[1].Name == u + "unitMonomial")
                {
                    return GetValue(unitedElements[0]);
                }
                else
                {
                    Debug.Fail("Unknown unitedValue");
                }
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "real")
            {
                return GetValue(elements[0]);
            }
            else if (elements.Count == 1 && elements[0].Name == ml + "matrix")
            {
                return ParseMatrix(elements[0]);
            }
            else
            {
                Debug.Fail("Unknown result");
            }

            return string.Empty;
        }

        private string ParseMatrix(XElement matrix)
        {
            int rows = 0;
            int cols = 0;
            var rowsAttribute = matrix.Attribute("rows");
            if (rowsAttribute != null)
            {
                rows = int.Parse(rowsAttribute.Value);
            }
            var colsAttribute = matrix.Attribute("cols");
            if (colsAttribute != null)
            {
                cols = int.Parse(colsAttribute.Value);
            }
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"matrix = Matrix<double>.Build.Dense({rows}, {cols});");
            var matrixElements = matrix.Elements().ToList();
            for (int i = 0; i < matrixElements.Count; i++)
            {
                var matrixElement = matrixElements[i];
                if (matrixElement.Name == ml + "real")
                {
                    stringBuilder.AppendLine($"matrix[{i % rows}, {i / rows}] = {GetValue(matrixElement)};");
                }
                else if (matrixElement.Name == ml + "complex")
                {
                    var real = matrixElement.Element(ml + "real");
                    var imag = matrixElement.Element(ml + "imag");
                    stringBuilder.AppendLine($"matrix[{i % rows}, {i / rows}] = new Complex({real.Value}, {imag.Value});");
                }
                else if (matrixElement.Name == ml + "apply")
                {
                    stringBuilder.AppendLine($"matrix[{i % rows}, {i / rows}] = {ParseApply(matrixElement)};");
                }
                else if (matrixElement.Name == ml + "parens")
                {
                    stringBuilder.AppendLine($"matrix[{i % rows}, {i / rows}] = {ParseParens(matrixElement)};");
                }
                else
                {
                    Debug.Fail("Unknown matrix element");
                }

            }

            return stringBuilder.ToString();
        }

        private string ParseSequence(XElement sequence)
        {
            var list = new List<string>();
            var sequenceElements = sequence.Elements().ToList();
            for (int i = 0; i < sequenceElements.Count; i++)
            {
                var sequenceElement = sequenceElements[i];
                if (sequenceElement.Name == ml + "real")
                {
                    list.Add(GetValue(sequenceElement));
                }
                else if (sequenceElement.Name == ml + "id")
                {
                    list.Add(GetValue(sequenceElement));
                }
                else if (sequenceElement.Name == ml + "apply")
                {
                    list.Add($"{ParseApply(sequenceElement)}");
                }
                else
                {
                    Debug.Fail("Unknown sequence element");
                }

            }

            return string.Join(", ", list);
        }

        private string ParseProgram(XElement program)
        {
            var stringBuilder = new StringBuilder();
            var elements = program.Elements().ToList();
            foreach (var element in elements)
            {
                if (element.Name == ml + "ifThen")
                {
                    stringBuilder.AppendLine(ParseIfThen(element));
                }
                else if (element.Name == ml + "apply")
                {
                    stringBuilder.AppendLine(ParseApply(element));
                }
                else
                {
                    Debug.Fail("Unknown program");
                }
            }
            return stringBuilder.ToString();
        }

        private string ParseIfThen(XElement ifThen)
        {
            var stringBuilder = new StringBuilder();
            var elements = ifThen.Elements().ToList();
            if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "real")
            {
                var applyElement = elements[0];
                var apply = ParseApply(applyElement);
                stringBuilder.AppendLine($"if({apply})");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"return {GetValue(elements[1])};");
                stringBuilder.AppendLine("}");
            }
            else if (elements.Count == 2 && elements[0].Name == ml + "apply" && elements[1].Name == ml + "program")
            {
                var applyElement = elements[0];
                var apply = ParseApply(applyElement);
                var program = ParseProgram(elements[1]);
                stringBuilder.AppendLine($"if({apply})");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"return {program};");
                stringBuilder.AppendLine("}");
            }
            else
            {
                Debug.Fail("Unknown ifthen");
            }
            return stringBuilder.ToString();
        }
    }
}
