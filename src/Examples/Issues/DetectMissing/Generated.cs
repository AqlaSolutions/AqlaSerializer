// Modified by Vladyslav Taranov for AqlaSerializer, 2016

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Option: missing-value detection (*Specified/ShouldSerialize*/Reset*) enabled
    
// Generated from: test.proto
namespace Examples.Issues.DetectMissing
{
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"TestUser")]
  public partial class TestUser : global::AqlaSerializer.IExtensible
  {
    public TestUser() {}
    

    private uint? _uid;
    [global::ProtoBuf.ProtoMember(1, IsRequired = false, Name=@"uid", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    public uint uid
    {
      get { return _uid?? default(uint); }
      set { _uid = value; }
    }
    [global::System.Xml.Serialization.XmlIgnore]
    [global::System.ComponentModel.Browsable(false)]
    public bool uidSpecified
    {
      get { return _uid != null; }
      set { if (value == (_uid== null)) _uid = value ? uid : (uint?)null; }
    }
    private bool ShouldSerializeuid() { return uidSpecified; }
    private void Resetuid() { uidSpecified = false; }
    /*

    private bool? _is_active;
    [global::ProtoBuf.ProtoMember(2, IsRequired = false, Name=@"is_active", DataFormat = global::ProtoBuf.DataFormat.Default)]
    public bool is_active
    {
      get { return _is_active?? default(bool); }
      set { _is_active = value; }
    }
    [global::System.Xml.Serialization.XmlIgnore]
    [global::System.ComponentModel.Browsable(false)]
    public bool is_activeSpecified
    {
      get { return _is_active != null; }
      set { if (value == (_is_active== null)) _is_active = value ? is_active : (bool?)null; }
    }
    private bool ShouldSerializeis_active() { return is_activeSpecified; }
    private void Resetis_active() { is_activeSpecified = false; }
    

    private string _name;
    [global::ProtoBuf.ProtoMember(3, IsRequired = false, Name=@"name", DataFormat = global::ProtoBuf.DataFormat.Default)]
    public string name
    {
      get { return _name?? ""; }
      set { _name = value; }
    }
    [global::System.Xml.Serialization.XmlIgnore]
    [global::System.ComponentModel.Browsable(false)]
    public bool nameSpecified
    {
      get { return _name != null; }
      set { if (value == (_name== null)) _name = value ? name : (string)null; }
    }
    private bool ShouldSerializename() { return nameSpecified; }
    private void Resetname() { nameSpecified = false; }
    */
    private global::AqlaSerializer.IExtension extensionObject;
    global::AqlaSerializer.IExtension global::AqlaSerializer.IExtensible.GetExtensionObject(bool createIfMissing)
      { return global::AqlaSerializer.Extensible.GetExtensionObject(ref extensionObject, createIfMissing); }
     
  }
  
}