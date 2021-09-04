namespace JsonData 


type ActionsDuringTurn = 
    {
        after_turn : TriggerTypes[]
        before_turn : TriggerTypes[]
        did_player_go_first : bool
        first_action : Action
        second_action : Action
    }