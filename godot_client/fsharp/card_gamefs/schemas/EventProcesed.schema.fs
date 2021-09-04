namespace JsonData 

type LandAction = 
    {
        image : string
    }
type TileType = 

    | Empty
    | End
    | Start
    | Basic
    | Fight
type TileAction = 
    {
        actions : LandAction[]
        can_leave : bool
        tile_type : TileType
    }

type EventProcesed = 

    | Error
    | Success of option<TileAction>
    | CurrentlyInAction of option<TileAction>
