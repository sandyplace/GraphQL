using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Graph.QL.Parser;
using Newtonsoft.Json.Linq;

namespace Graph.QL
{
    public class Resolver : IResolver
    {
        public void Resolve(IContext typeContext, IObjectType objectType)
        {
            
        }
    }

    public interface IContext
    {
        JContainer      Parent { get; }
        ISchema         Schema { get; }
    }

    public interface IResolver
    {
        void            Resolve(IContext typeContext, IObjectType objectType);
    }


    class Program
    {
        static void Main(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            const string q = @"
query HeroNameQuery {
  hero {
    name
  }
}    
";

            const string s = @"
enum Episode { NEWHOPE, EMPIRE, JEDI }

interface Character {
  id: String!
  name: String
  friends: [Character]
  appearsIn: [Episode]
}

type Human implements Character {
  id: String!
  name: String
  friends: [Character]
  appearsIn: [Episode]
  homePlanet: String
}

type Droid implements Character {
  id: String!
  name: String
  friends: [Character]
  appearsIn: [Episode]
  primaryFunction: String
}

type Query {
  hero(episode: Episode): Character
  human(id: String!): Human
  droid(id: String!): Droid
}";

            StringReader        inputStream     = new StringReader(s);
            AntlrInputStream    input           = new AntlrInputStream(inputStream.ReadToEnd());
            GraphQLSchemaLexer  lexer           = new GraphQLSchemaLexer(input);
            CommonTokenStream   tokens          = new CommonTokenStream(lexer);
            GraphQLSchemaParser parser          = new GraphQLSchemaParser(tokens);
            var                 documentContext = parser.document();
            Console.WriteLine(documentContext.ToStringTree());
            ISchema             schema          = new GraphQLSchemaVisitor().Visit(documentContext);

            ParseQuery(schema, q);
        }

        static void             ParseQuery(ISchema schema, string s)
        {
            var               query           = schema.getType("Query");
            StringReader      inputStream     = new StringReader(s);
            AntlrInputStream  input           = new AntlrInputStream(inputStream.ReadToEnd());
            GraphQLLexer      lexer           = new GraphQLLexer(input);
            CommonTokenStream tokens          = new CommonTokenStream(lexer);
            GraphQLParser     parser          = new GraphQLParser(tokens);
            var               documentContext = parser.document();
            new GraphQLVisitor(schema).Visit(documentContext);
        }
    }


    public class GraphQLVisitor : GraphQLBaseVisitor<IContext>
    {
        readonly ISchema        _schema;
        IResolver               _resolver;
        Context                 _context;

        public                  GraphQLVisitor(ISchema schema)
        {
            _schema   = schema;
            _resolver = new Resolver();
            _context  = new Context(_schema);
        }

        public override IContext VisitOperationDefinition(GraphQLParser.OperationDefinitionContext context)
        {
            var operationType = context.operationType().GetText();
            if(operationType == "query")
            {
                var typeName = context.NAME().GetText();
                _context.Push(typeName);
                var type = _schema.getType(typeName);
                _resolver.Resolve(_context, type as IObjectType);
                _context.Pop();
                return _context;
            }
            return _context;
        }

        private class Context : IContext
        {
            public              Context(ISchema schema)
            {
                Parent  = new JObject();
                Root    = new JObject {["Data"] = Parent};
                Schema  = schema;
            }

            public void         Push(string name)
            {
                var newParent = new JObject();
                Parent[name]  = newParent;
                Parent        = newParent;
            }

            public void        Pop()
            {
                Parent = Parent.Parent;
            }

            private JContainer  Root { get; set; }
            public JContainer   Parent { get; private set; }
            public ISchema      Schema { get; }
        }
    }

    public class GraphQLSchemaVisitor : GraphQLSchemaBaseVisitor<ISchema>
    {
        readonly Schema     _schema;
        InterfaceType       _currentType;

        public              GraphQLSchemaVisitor()
        {
            _schema = new Schema();
            _currentType = null;
        }

        public override ISchema Visit(IParseTree tree)
        {
            base.Visit(tree);
            return _schema;
        }

        public override ISchema VisitField(GraphQLSchemaParser.FieldContext context)
        {
            string fieldName = context.NAME().ToString();
            var typeContext  = context.fieldDefinition().type();
            var typeInfo     = this.getType(typeContext);

            var argumentsContext = context.arguments();
            if (argumentsContext == null)
            {
                var isRequired  = typeContext.nonNullType() != null;
                _currentType.add(new Property(fieldName, typeInfo, isRequired));
                return _schema;
            }

            List<IParameter> parameters = new List<IParameter>();
            foreach (var argumentContext in argumentsContext.argument())
            {
                var parameterName = argumentContext.NAME().ToString();
                var parameterType = this.getType(argumentContext.type());
                var isRequired    = argumentContext.type().nonNullType() != null;
                parameters.Add(new Parameter(parameterName, parameterType, isRequired));
                
            }

            _currentType.add(new Method(fieldName, typeInfo, parameters));
            return _schema;
        }

        public IType            getType(GraphQLSchemaParser.TypeContext typeContext)
        {
            var typeNameContext = typeContext.typeName();
            if (typeNameContext != null)
                return _schema.getType(typeNameContext.NAME().ToString());

            var listTypeContext = typeContext.listType();
            if (listTypeContext == null)
                return null;

            IType typeInfo = getType(listTypeContext.type());
            return new ListType("list", typeInfo);
        }

        public override ISchema VisitEnumDefinition(GraphQLSchemaParser.EnumDefinitionContext context)
        {
            var typeName = context.NAME().ToString();
            var typeInfo = new EnumType(typeName);
            foreach (var e in context.enumBodyDefinition().NAME())
            {
                typeInfo.add(new Property(e.ToString(), ScalarType.String, false));
            }
            _schema.add(typeInfo);
            return _schema;
        }

        public override ISchema VisitTypeDefinition(GraphQLSchemaParser.TypeDefinitionContext context)
        {
            _currentType = new ObjectType(context.NAME().ToString());
            _schema.add(_currentType);
            return base.VisitTypeDefinition(context);
        }

        public override ISchema VisitInterfaceDefinition(GraphQLSchemaParser.InterfaceDefinitionContext context)
        {
            _currentType = new InterfaceType(context.NAME().ToString());
            _schema.add(_currentType);
            return base.VisitInterfaceDefinition(context);
        }
    }
}
