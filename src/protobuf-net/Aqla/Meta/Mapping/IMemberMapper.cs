#if !NO_RUNTIME
namespace AqlaSerializer.Meta.Mapping
{
    public interface IMemberMapper
    {
        MappedMember Map(MemberArgsValue args);
    }
}
#endif
