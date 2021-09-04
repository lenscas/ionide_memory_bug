namespace JsonData 

type BattleErrorsCardCostsTooMuch = 
    {
        chosen : int
        mana_available : int
        mana_needed : int
    }

type BattleErrors = 

    | ChosenCardNotInHand of int
    | CardCostsTooMuch of BattleErrorsCardCostsTooMuch
