[![NuGet Status](https://img.shields.io/nuget/v/Form.svg?style=flat)](https://www.nuget.org/packages/Form/)

# F# Object Relational Mapper

An attribute based ORM for F#.

## [Documentation](https://hillcrest-r-d.github.io/FORM/)

## Development Slow-Down

HCRD has decided to take a step back from development on FORM for the time-being. If anyone wishes to take up the mantle of development, we will be glad to provide direction and feedback. We will, of course, perform bug-fixes if any are discovered. 

## Docket

The team was working on implementing a Relationship type that would abstract away the query to load child records in a 1:many relationship. We do not aim to support a 1:1 relationship because you can just create a record composed of the fields of other records by using the `ByJoin` attribute. Supporting a many:many relationship would require a completely different approach to how queries are constructed; and, also, it's not something we're aiming to support.

Ideally, we wish to be able to support inserting, updating, and deleting (maybe) on these relations as well. The user should be able to supply a flag that allows either the library to query the db for the related records when the data is being read into memory or allow the user to force the evaluation at the time they deem fit -- similar to lazy evaluation. 

All the work that has been done will remain on the dev branches. However, we're doing some restructuring of the Git history to make things a bit more linear in terms of versioning.

Eventually, we will come back to FORM to finish it, if it's not already done by that time. We have other projects that need our attention 