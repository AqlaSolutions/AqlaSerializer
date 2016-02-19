#if !NO_RUNTIME
namespace AqlaSerializer.Meta.Mapping
{
    public interface IMemberMapper
    {
        MappedMember Map(ref MemberArgsValue args);
    }
}
#endif
