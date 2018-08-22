using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support
{
  public static class SearchErrorMessages
  {
    // Fields
    public const string NodeTypeNotSupported = "Nodes of type '{0}' are not supported";
    public const string TypeNotSupportedInAnyFunction = "The '{0}' type is not supported in 'any' function.";
    public const string FiltrationOfPropertyNotSupported = "Filtration of the '{0}' is not supported.";
    public const string BodyKindOfAnyFunctionNotSupported = "The '{0}' body kind of 'any' function is not supported.";
    public const string FunctionParametersOfUnsupportedFormat = "Parameters of the '{0}' function are of unsupported format.";
    public const string OperatorNotSupported = "The '{0}' operator is not supported.";
    public const string OperatorsNotSupported = "The '{0}' or '{1}' operators are not supported.";
    public const string PropertyNotSupportedInFunction = "The '{0}' is not supported in the '{1}' function.";
    public const string FunctionNotSupported = "The '{0}' function is not supported.";
    public const string SyntaxNearPropertyNotSupported = "Syntax near the '{0}' property is not supported.";
    public const string NullOperandNotSupported = "The null operand is not supported for the '{0}' operator.";
  }
}