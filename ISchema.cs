using System.Collections.Generic;

namespace Graph.QL
{
    public interface IField
    {
        string                  name { get; }
        IType                   type { get;  }
    }

    public interface IProperty : IField
    {
        bool                    isRequired { get; }
    }

    public interface IParameter : IProperty { }

    public interface IMethod : IField
    {
        IReadOnlyList<IParameter> parameters { get; }
    }

    public interface IType
    {
        string                  name { get; }

        IEnumerable<IProperty>  properties { get; }
        IProperty               getProperty(string name);
    }

        public interface IInterfaceType : IType
    {
        IEnumerable<IMethod>    methods { get; }
        IMethod                 getMethod(string name);
    }

    public interface IListType : IType
    {
        IType                   Type { get; } 
    }

    public interface IScalarType : IType { }

    public interface IObjectType : IInterfaceType { }

    public interface IEnumType : IType { }

    public interface ISchema
    {
        IEnumerable<IType>      types { get; }
        IType                   getType(string name);
    }
}