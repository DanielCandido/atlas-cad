using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Atlas.Cad.Core.Styles;

[Flags]
[DataContract]
public enum MlineStyleFlags
{
    [EnumMember]
    None = 0,
    [EnumMember]
    Filled = 1,
    [EnumMember]
    ShowMiters = 2,
    [EnumMember]
    StartSquareCap = 16,
    [EnumMember]
    StartInnerArcs = 32,
    [EnumMember]
    StartRoundCap = 64,
    [EnumMember]
    EndSquareCap = 256,
    [EnumMember]
    EndInnerArcs = 512,
    [EnumMember]
    EndRoundCap = 1024
}

[DataContract]
public sealed class MlineStyleDefinition
{
    [DataMember(Name = "name", IsRequired = true, Order = 1)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "description", IsRequired = true, Order = 2)]
    public string Description { get; set; } = string.Empty;

    [DataMember(Name = "flags", IsRequired = true, Order = 3)]
    public MlineStyleFlags Flags { get; set; }

    [DataMember(Name = "fillColorIndex", IsRequired = true, Order = 4)]
    public int FillColorIndex { get; set; } = 256;

    [DataMember(Name = "startAngleDegrees", IsRequired = true, Order = 5)]
    public double StartAngleDegrees { get; set; } = 90.0;

    [DataMember(Name = "endAngleDegrees", IsRequired = true, Order = 6)]
    public double EndAngleDegrees { get; set; } = 90.0;

    [DataMember(Name = "elements", IsRequired = true, Order = 7)]
    public List<MlineStyleElementDefinition> Elements { get; set; } = new List<MlineStyleElementDefinition>();
}

[DataContract]
public sealed class MlineStyleElementDefinition
{
    [DataMember(Name = "offset", IsRequired = true, Order = 1)]
    public double Offset { get; set; }

    [DataMember(Name = "colorIndex", IsRequired = true, Order = 2)]
    public int ColorIndex { get; set; } = 256;

    [DataMember(Name = "linetypeName", IsRequired = true, Order = 3)]
    public string LinetypeName { get; set; } = "BYLAYER";
}

