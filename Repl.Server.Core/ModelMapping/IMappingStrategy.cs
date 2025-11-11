namespace Repl.Server.Core.ProtocolMapping;

public interface IMappingStrategy<TInternal, TPublic>
{
    TPublic ToPublic(TInternal internalModel);
    TInternal ToInternal(TPublic publicModel);
}