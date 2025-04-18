namespace Form

module Types =

    exception UndefinedBehaviorException of msg: string

    exception KeylessTypeException of string * System.Type

    exception NoResultsException of msg: string
