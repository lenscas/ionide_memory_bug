namespace JsonData 

type BasicVector_for_uint = 
    {
        x : int
        y : int
    }

type DungeonLayout = 
    {
        height : int
        player_at : BasicVector_for_uint
        tiles : TileState[]
        widht : int
    }