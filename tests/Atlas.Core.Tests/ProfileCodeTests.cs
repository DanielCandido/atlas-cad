using Atlas.Cad.Core;
using Xunit;
public class ProfileCodeTests
{
    [Theory]
    [InlineData("U4\"X9.3", "U4X93")]
    [InlineData(" u4 x9,3 ", "U4X93")]
    public void NormalizesProfile(string input, string expected) => Assert.Equal(expected, ProfileCode.Normalize(input));
}
