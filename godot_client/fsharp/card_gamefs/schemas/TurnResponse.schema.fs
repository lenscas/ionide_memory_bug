namespace JsonData 


type TurnResponse = 

    | Done
    | NextTurn of ActionsDuringTurn
    | Error of BattleErrors
