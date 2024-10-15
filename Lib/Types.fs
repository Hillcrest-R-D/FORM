namespace Form

module Types =

    exception UndefinedBehaviorException of string

    exception KeylessTypeException of string * System.Type
