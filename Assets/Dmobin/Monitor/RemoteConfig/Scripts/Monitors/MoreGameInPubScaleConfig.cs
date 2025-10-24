using System;
using DSDK.Remote;
[Serializable]
public class MoreGameInPubScaleConfig : RemoteConfig<MoreGameInPubScaleConfig>
{
    public override string Key => "MoreGameInPubScale";
    public string[] ListImageLink = { "https://drive.google.com/uc?export=download&id=1AqjlpBcB669uGeAqOBPkIey9MO_KCl4k" };
    public string[] ListSizeImage = { "S268x316" };
    public string[] ListGameLink = { "https://app.adjust.com/1lhxhh0o?campaign=WeAreImpostorr" };
    public string[] ListIdGame = { "GC24_001" };
}

public enum TypeSizeImgMoreGamePubScale
{
    S268x316
}
