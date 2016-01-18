using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Graph.QL.Parser;

namespace Graph.QL
{
    public abstract class Field : IField
    {
        protected               Field(string name, IType type)
        {
            this.name = name;
            this.type = type;
        }

        public string           name { get; }
        public IType            type { get; }
    }

    public class Property : Field, IProperty
    {
        public                  Property(string name, IType type, bool isRequired) : base(name, type)
        {
            this.isRequired = isRequired;
        }

        public override string ToString()
        {
            return $"{name}:{type}{(isRequired ? "!" : string.Empty)}";
        }

        public bool             isRequired { get; }
    }

    public class Parameter : Property, IParameter
    {
        public                  Parameter(string name, IType type, bool isRequired) : base(name, type, isRequired) { }
    }

    public class BaseType : IType
    {
        readonly Dictionary<string, IProperty> _properties = new Dictionary<string, IProperty>();

        protected               BaseType(string name) { this.name = name; }

        public void             add(IProperty property) => _properties.Add(property.name, property);

        public string           name { get; }
        public IEnumerable<IProperty> properties => _properties.Values;
        public IProperty        getProperty(string n) => _properties.Get(n);


        public override string  ToString()
        {
            return $"{name}";
        }
    }

    public class EnumType : BaseType, IEnumType
    {
        public                  EnumType(string name) : base(name) { }
    }

    public class ScalarType : BaseType, IScalarType
    {
        public                  ScalarType(string name, Type type) : base(name)
        {
            Type = type;
        }

        public Type             Type { get; }
        public static readonly IType String     = new ScalarType("String", typeof(string));
        public static readonly IType Int        = new ScalarType("Int",    typeof(int));
        public static readonly IType Boolean    = new ScalarType("Bool",   typeof(bool));
        public static readonly IType Float      = new ScalarType("Float",  typeof(double));
        public static readonly IType ID         = new ScalarType("ID",     typeof(string));

        public static IEnumerable<IType> DefaultTypes = new[] { String, Int, Boolean, Float, ID }; 
    }

    public class InterfaceType : BaseType, IInterfaceType
    {
        readonly Dictionary<string, IMethod> _methods = new Dictionary<string, IMethod>();
        public                  InterfaceType(string name) : base(name) { }

        public void             add(IMethod method) => _methods.Add(method.name, method);
        public IEnumerable<IMethod> methods => _methods.Values;
        public IMethod          getMethod(string n) => _methods.Get(n);
    }

    public class ObjectType : InterfaceType, IObjectType
    {
        public                  ObjectType(string name) : base(name) { }
    }

    public class ListType : BaseType, IListType
    {
        public                  ListType(string name, IType listType) : base(name) { Type = listType; }
        public IType            Type { get; }
        public override string  ToString()
        {
            return $"[{Type}]";
        }
    }

    public class Method : Field, IMethod
    {
        public                  Method(string name, IType type, List<IParameter> parameters) : base(name, type)
        {
            this.parameters  = parameters.AsReadOnly();
        }

        public override string  ToString()
        {
            var ps = string.Join(",", parameters.Select(p => $"{p.name}:{p.type}{(p.isRequired ? "!" : string.Empty)}"));
            return $"{name}({ps}):{this.type}";
        }

        public IReadOnlyList<IParameter> parameters { get; }
    }

    public class Schema : ISchema
    {
        public                  Schema()
        {
            ScalarType.DefaultTypes.Do(add);
        }

        public void             add(IType type) => _types.Add(type.name, type);

        readonly IDictionary<string, IType> _types = new Dictionary<string, IType>(); 

        public IEnumerable<IType> types => _types.Values;

        public IType            getType(string name) => _types.Get(name);

        public static ISchema   Create(string schema)
        {
            return Create(new StringReader(schema));
        }

        public static ISchema   Create(TextReader inputStream)
        {
            var input           = new AntlrInputStream(inputStream.ReadToEnd());
            var lexer           = new GraphQLSchemaLexer(input);
            var tokens          = new CommonTokenStream(lexer);
            var parser          = new GraphQLSchemaParser(tokens);
            var documentContext = parser.document();
            return new GraphQLSchemaVisitor().Visit(documentContext);
        }
    }

    public static class SchemaExtensions
    {
        public static R         Get<K,R>(this IDictionary<K,R> d, K key)
        {
            R value;
            d.TryGetValue(key, out value);
            return value;
        }

        public static void      Do<T>(this IEnumerable<T> source, Action<T> action)
        {
            if(source == null)
                throw new ArgumentNullException(nameof(source));

            foreach (T element in source)
                action(element);
        }
    }
}