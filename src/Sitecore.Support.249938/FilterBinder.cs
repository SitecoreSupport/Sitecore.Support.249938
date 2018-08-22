// © 2017 Sitecore Corporation A/S. All rights reserved. Sitecore® is a registered trademark of Sitecore Corporation A/S.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.OData.Query;
using Microsoft.OData.UriParser;
using Sitecore.Support;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Services.Infrastructure.Sitecore.Data;
using Sitecore.StringExtensions;
using Sitecore.Content.Services.Items.OData.Search;

namespace Sitecore.Suport.Content.Services.Items.OData.Search
{
  public class FilterBinder : Sitecore.Content.Services.Items.OData.Search.FilterBinder
  {
    [NotNull]
    private readonly ComparisonExpressionBuilder _comparisonBuilder;

    [NotNull]
    private readonly FieldNameResolver _fieldNameResolver;

    private readonly SearchHelper _searchHelper;

    private bool _isLanguageFiltered;

    public FilterBinder([NotNull] ComparisonExpressionBuilder comparisonBuilder, [NotNull] FieldNameResolver fieldNameResolver, [NotNull] SearchHelper searchHelper) : base(comparisonBuilder, fieldNameResolver, searchHelper)
    {
      Assert.ArgumentNotNull(comparisonBuilder, nameof(comparisonBuilder));
      Assert.ArgumentNotNull(fieldNameResolver, nameof(fieldNameResolver));
      Assert.ArgumentNotNull(searchHelper, nameof(searchHelper));

      _comparisonBuilder = comparisonBuilder;
      _fieldNameResolver = fieldNameResolver;
      _searchHelper = searchHelper;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "By design")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "The PredicateBuilder is used. It returns the Expression<Func<T, bool>> result.")]
    [NotNull]
    public override Expression<Func<FullTextSearchResultItem, bool>> BindFilterQuery<T>([CanBeNull] FilterClause filter, [CanBeNull] string language, [CanBeNull] string version)
    {
      Expression<Func<FullTextSearchResultItem, bool>> expression = filter != null ? Bind<T>(filter.Expression) : _ => true;

      if (!string.IsNullOrEmpty(version))
      {
        expression = expression.And(x => (string)x[(ObjectIndexerKey)"_version"] == version);
      }

      if (!string.IsNullOrEmpty(language))
      {
        expression = expression.And(x => x.Language == language);
      }
      else if (!_isLanguageFiltered)
      {
        expression = expression.And(x => x.Language == LanguageManager.DefaultLanguage.Name);
      }

      return expression;
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> Bind<T>([NotNull] SingleValueNode node, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames = null)
    {
      switch (node.Kind)
      {
        case QueryNodeKind.BinaryOperator:
          return BindBinaryOperatorNode<T>(node as BinaryOperatorNode, resolvedFiledNames);

        case QueryNodeKind.Convert:
          return BindConvertNode<T>(node as ConvertNode, resolvedFiledNames);

        case QueryNodeKind.UnaryOperator:
          return BindUnaryOperatorNode<T>(node as UnaryOperatorNode, resolvedFiledNames);

        case QueryNodeKind.SingleValueFunctionCall:
          return BindSingleValueFunctionCallNode<T>(node as SingleValueFunctionCallNode, resolvedFiledNames);

        case QueryNodeKind.Any:
          return BindAnyNode<T>(node as AnyNode);

        default:
          throw new NotSupportedException(SearchErrorMessages.NodeTypeNotSupported.FormatWith(node.Kind));
      }
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> BindAnyNode<T>(AnyNode anyNode)
    {
      Assert.ArgumentNotNull(anyNode, nameof(anyNode));

      var sourceNode = anyNode.Source as CollectionResourceNode;
      if (sourceNode == null)
      {
        throw new NotSupportedException(SearchErrorMessages.TypeNotSupportedInAnyFunction.FormatWith(anyNode.Source.GetType()));
      }

      string sourceName = sourceNode.NavigationSource.Name;

      PropertyInfo property = typeof(T).GetProperty(sourceName);
      if (property == null || property.GetCustomAttribute<FilterAttribute>(true)?.Disabled == true)
      {
        throw new NotSupportedException(SearchErrorMessages.FiltrationOfPropertyNotSupported.FormatWith(sourceName));
      }

      var binaryNode = anyNode.Body as BinaryOperatorNode;
      if (binaryNode == null)
      {
        throw new NotSupportedException(SearchErrorMessages.BodyKindOfAnyFunctionNotSupported.FormatWith(anyNode.Body.Kind));
      }

      Dictionary<SingleValueNode, string> resolvedFiledNames = _fieldNameResolver.ResolveFiledNames(binaryNode);

      return BindBinaryOperatorNode<T>(binaryNode, resolvedFiledNames);
    }

    [NotNull]
    private PropertyExpressionData GetStringFunctionExpression<T>([NotNull] SingleValueFunctionCallNode singleValueFunctionCallNode, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames)
    {
      SingleValueNode leftNode = null;
      SingleValueNode rightNode = null;

      List<QueryNode> arguments = singleValueFunctionCallNode.Parameters.ToList();
      if (arguments.Count > 1)
      {
        leftNode = arguments[0] as SingleValueNode;
        rightNode = arguments[1] as SingleValueNode;
      }

      if (leftNode == null || rightNode == null)
      {
        throw new NotSupportedException(SearchErrorMessages.FunctionParametersOfUnsupportedFormat.FormatWith(singleValueFunctionCallNode.Name));
      }

      PropertyExpressionData expressionData = GetPropertyExpressionData<T>(leftNode, rightNode, resolvedFiledNames, singleValueFunctionCallNode);
      if (expressionData == null)
      {
        throw new NotSupportedException(SearchErrorMessages.OperatorsNotSupported.FormatWith(arguments[0].Kind, arguments[1].Kind));
      }

      if (expressionData.IsFiledName || !expressionData.IsSupported)
      {
        throw new NotSupportedException(SearchErrorMessages.PropertyNotSupportedInFunction.FormatWith(expressionData.Name, singleValueFunctionCallNode.Name));
      }

      return expressionData;
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> BindSingleValueFunctionCallNode<T>(SingleValueFunctionCallNode singleValueFunctionCallNode, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames)
    {
      Assert.ArgumentNotNull(singleValueFunctionCallNode, nameof(singleValueFunctionCallNode));

      PropertyExpressionData expressionData;
      string name;
      string value;

      switch (singleValueFunctionCallNode.Name)
      {
        case "contains":
          expressionData = GetStringFunctionExpression<T>(singleValueFunctionCallNode, resolvedFiledNames);

          name = expressionData.Name;
          value = expressionData.Value.ToString();

          return item => item[name].Contains(value);

        case "endswith":
          expressionData = GetStringFunctionExpression<T>(singleValueFunctionCallNode, resolvedFiledNames);

          name = expressionData.Name;
          value = expressionData.Value.ToString();

          return item => item[name].EndsWith(value);

        case "startswith":
          expressionData = GetStringFunctionExpression<T>(singleValueFunctionCallNode, resolvedFiledNames);

          name = expressionData.Name;
          value = expressionData.Value.ToString();

          return item => item[name].StartsWith(value);

        default:
          throw new NotSupportedException(SearchErrorMessages.FunctionNotSupported.FormatWith(singleValueFunctionCallNode.Name));
      }
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> BindUnaryOperatorNode<T>(UnaryOperatorNode unaryOperatorNode, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames)
    {
      Assert.ArgumentNotNull(unaryOperatorNode, nameof(unaryOperatorNode));

      if (unaryOperatorNode.OperatorKind == UnaryOperatorKind.Not)
      {
        return Bind<T>(unaryOperatorNode.Operand, resolvedFiledNames).Not();
      }

      throw new NotSupportedException(SearchErrorMessages.OperatorNotSupported.FormatWith(unaryOperatorNode.OperatorKind));
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> BindConvertNode<T>(ConvertNode convertNode, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames)
    {
      Assert.ArgumentNotNull(convertNode, nameof(convertNode));

      return Bind<T>(convertNode.Source, resolvedFiledNames);
    }

    [NotNull]
    private Expression<Func<FullTextSearchResultItem, bool>> BindBinaryOperatorNode<T>(BinaryOperatorNode binaryOperatorNode, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames)
    {
      Assert.ArgumentNotNull(binaryOperatorNode, nameof(binaryOperatorNode));

      PropertyExpressionData expressionData = GetPropertyExpressionData<T>(binaryOperatorNode.Left, binaryOperatorNode.Right, resolvedFiledNames);
      if (expressionData != null)
      {
        if (expressionData.IsFiledName)
        {
          return PredicateBuilder.True<FullTextSearchResultItem>();
        }

        if (!expressionData.IsSupported)
        {
          throw new NotSupportedException(SearchErrorMessages.SyntaxNearPropertyNotSupported.FormatWith(expressionData.Name));
        }

        return _comparisonBuilder.BuildExpression(binaryOperatorNode.OperatorKind, expressionData.Name, expressionData.Value);
      }

      Expression<Func<FullTextSearchResultItem, bool>> left = Bind<T>(binaryOperatorNode.Left, resolvedFiledNames);
      Expression<Func<FullTextSearchResultItem, bool>> right = Bind<T>(binaryOperatorNode.Right, resolvedFiledNames);

      switch (binaryOperatorNode.OperatorKind)
      {
        case BinaryOperatorKind.And:
          return left.And(right);

        case BinaryOperatorKind.Or:
          return left.Or(right);

        default:
          throw new NotSupportedException(SearchErrorMessages.OperatorNotSupported.FormatWith(binaryOperatorNode.OperatorKind));
      }
    }

    [CanBeNull]
    private PropertyExpressionData GetPropertyExpressionData<T>([NotNull] SingleValueNode left, [NotNull] SingleValueNode right, [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames, [CanBeNull] SingleValueFunctionCallNode functionCallNode = null)
    {
      var result = new PropertyExpressionData();

      ResolvePropertyExpressionDataName<T>(result, left, resolvedFiledNames, functionCallNode);
      if (result.Name == null)
      {
        return null;
      }

      ResolvePropertyExpressionDataValue(result, right);
      if (result.Value == null)
      {
        return null;
      }

      if (result.Name == "Id")
      {
        Guid id;
        if (Guid.TryParse(result.Value.ToString(), out id))
        {
          result.Name = BuiltinFields.Group;
          result.Value = id;
        }
      }

      return result;
    }

    private void ResolvePropertyExpressionDataName<T>(
        [NotNull] PropertyExpressionData propertyExpressionData,
        [NotNull] SingleValueNode node,
        [CanBeNull] Dictionary<SingleValueNode, string> resolvedFiledNames,
        [CanBeNull] SingleValueFunctionCallNode functionCallNode)
    {
      node = ConvertIfNecessary(node);

      string propertyName = GetPropertyName(node);
      if (string.IsNullOrEmpty(propertyName))
      {
        return;
      }

      if (resolvedFiledNames == null)
      {
        propertyExpressionData.Name = GetIndexableProperty<T>(propertyName);
        return;
      }

      switch (propertyName)
      {
        case "Name":
          propertyExpressionData.IsFiledName = true;
          propertyExpressionData.Name = propertyName;
          break;

        case "Value":
          resolvedFiledNames.TryGetValue(node, out propertyExpressionData.Name);

          // When a function is used inside of "any" operator a new propertyNode is created each time when it is requested.
          // Therefore it is not possible to find it in resolvedFiledNames dictionary. The function node is used in this case.
          if (string.IsNullOrEmpty(propertyExpressionData.Name) && functionCallNode != null)
          {
            resolvedFiledNames.TryGetValue(functionCallNode, out propertyExpressionData.Name);
          }

          if (propertyExpressionData.Name != null)
          {
            propertyExpressionData.Name = propertyExpressionData.Name.Replace(' ', '_');
          }

          break;

        default:
          propertyExpressionData.IsSupported = false;
          propertyExpressionData.Name = propertyName;
          break;
      }
    }

    [NotNull]
    private string GetIndexableProperty<T>([NotNull] string propertyName)
    {
      if (propertyName == "Language")
      {
        _isLanguageFiltered = true;
      }

      return _searchHelper.GetIndexFieldName(typeof(T), propertyName);
    }

    private static void ResolvePropertyExpressionDataValue([NotNull] PropertyExpressionData propertyExpressionData, [NotNull] SingleValueNode node)
    {
      node = ConvertIfNecessary(node);

      var constantNode = node as ConstantNode;
      if (constantNode != null)
      {
        propertyExpressionData.Value = constantNode.Value;
      }
    }

    [NotNull]
    private static SingleValueNode ConvertIfNecessary([NotNull] SingleValueNode node)
    {
      var convertNode = node as ConvertNode;
      return convertNode != null ? convertNode.Source : node;
    }

    [CanBeNull]
    private static string GetPropertyName([NotNull] SingleValueNode node)
    {
      switch (node.Kind)
      {
        case QueryNodeKind.SingleValuePropertyAccess:
          return (node as SingleValuePropertyAccessNode)?.Property.Name;

        case QueryNodeKind.ResourceRangeVariableReference:
          return (node as ResourceRangeVariableReferenceNode)?.Name;

        default:
          return null;
      }
    }

    private class PropertyExpressionData
    {
      public string Name;
      public object Value;
      public bool IsFiledName;
      public bool IsSupported = true;
    }
  }
}
