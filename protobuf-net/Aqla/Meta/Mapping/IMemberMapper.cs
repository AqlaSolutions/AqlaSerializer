namespace AqlaSerializer.Meta.Mapping
{
    public interface IMemberMapper
    {
        NormalizedProtoMember Read(MemberArgsValue args);
    }
}