namespace JsonData 


type Action = 
    {
        taken_action : PossibleActions
        triggered_after : TriggerTypes[]
        triggered_before : TriggerTypes[]
    }