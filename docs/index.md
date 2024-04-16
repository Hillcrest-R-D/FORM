# Form

Form is an attribute-based object-relational mapper specifically for F#'s record types. The design goals of the library, in order, are:

- Correctness & Safety
- Minimal & Ergonomic
- Performant

## Correctness & Safety

Have you ever used a library/framework where you call an innocent-looking function, but, little did you know, it has this long chain of function calls and *somewhere* in the call-stack one of those functions threw an exception that was never caught which causes your business-critical system to crash because... how were you supposed to know? We have... and we ***hated*** it. You don't deserve that headache. Form aims to catch every possible exception that could be thrown under any of its top-level functions and present the user with a nice `Result< 't, exn >` (or `Seq<Result< 't, exn >>` in the case of reading results) so *you* are in control of how that error gets handled. Any exception message (and so any stack trace) will get passed forward to the end-programmer, avoiding the need for try/catch statements in the data functions of application-level code.

We also like to promote "correct" approaches: 
- Make things as type-safe as possible (but no more)
- Limit patterns that pose security risks


## Minimal & Ergonomic

> Here, functional can be replaced with object-oriented or even procedural.

Orms are janky. This is partly due to the fact that a relational model and a functional model just do not have a 1:1 mapping... see what we did there :). Sometimes, there is no efficient/ergonomic functional equivalent. Form aims to be ergonomic. We *don't* want it to be janky. That also means there are certain areas where we must make compromises. There will be features where we will have absolutely no intention on supporting. 

Even though there are features we don't plan on supporting, there is a good subset we aim to be exceptional at. Basic CRUD is a breeze. This includes avoiding pitfalls from naive implementations of bulk operations.


## Performant

Form is built on top of ADO.Net. While we can't be as fast as ADO, we want to be as close to it as possible. In our benchmarking, FORM has comparable performance to Dapper and is even faster in certain scenarios. This is achieved through very precise query generation and caching strategies, along with minimizing allocations and lazy-loading the query result. 

## Let's Get Started!

With these design goals in mind, we think you're ready to start using [FORM](./basics.md)