namespace JsonData 

type SpritePosition = 
    {
        height : float
        offsets : option<float[]>
        width : float
        x : float
        y : float
    }

type SerializedSpriteSheet = 
    {
        sprites : SpritePosition[]
        texture_height : float
        texture_width : float
    }