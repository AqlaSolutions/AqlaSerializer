namespace AqlaSerializer.Meta.Mapping
{
    public interface IMemberMapper
    {
        NormalizedMappedMember Map(ref MemberArgsValue args);
    }
}