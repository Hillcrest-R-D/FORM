# Advanced

There's really only one more thing to learn about FORM. As we said, the insert, update, and delete function always have a transaction associated with them -- whether provided by you or created by them. If you have a sequence of items you're performing these actions on, it becomes extremely inefficient to constantly create and commit these transactions (and to also make all the network trips). So we've given you some functions specifically for sequences of data.

Using the setup from the last page, instead of calling insert for each record, we can simply write it like this

```fsharp
Form.Orm.insertMany state None true myUsers
```

This way, only one transaction is created for the whole operation AND we also auto-batch data sent over the wire in these so there are fewer network trips. Ultimately, this means a massive speedup over naively iterating over the sequence and calling insert on each record.