# Verbose JSON Parser
VJP is a JSON parser and generator which heavily relies on type system.  
**NOTE:** this package depends on [Option](https://github.com/codeRiftel/option).  

## Less verbose option
If you're willing to sacrifice some speed you can check out [vjp-reflect](https://github.com/codeRiftel/vjp-reflect). It uses reflection to convert from object to JSONType and vice versa.  
Don't want to sacrifice speed? Check out [meta-vjp](https://github.com/codeRiftel/meta-vjp) which generates C# code for your lazy ass.

## Why?
Unity has very bad support for JSON and I have to use Unity, so I decided to make my life a little bit less miserable by creating my own parser.

## Features
* uses C# 2.0
* codebase is about 800 SLOC
* no OOP
* no exceptions
* heavy usage of type system

## Usage
Suppose you have this JSON in a **string** named **input**
```javascript
{
    "name": "foo",
    "age": 42,
    "dumb": true,
    "credentials": null,
    "repos": ["bar1", "bar2"]
}
```
Let's parse it.
```csharp
// second parameter represents depth limit
Result<JSONType, JSONError> typeRes = VJP.Parse(input, 1024);
if (typeRes.IsErr()) {
    JSONError error = typeRes.AsErr();
    // do something about this error
} else {
    JSONType type = typeRes.AsOk();
    // we know that root type in this case is an object, so we check for it
    if (type.Obj.IsSome()) {
        // object is a Dictionary, you should be familiar with it
        Dictionary<string, JSONType> obj = type.Obj.Peel();
        // does it contain "name" field?
        if (obj.ContainsKey("name")) {
            JSONType nameType = obj["name"];
            // we know that name must be string, so we check for it
            if (nameType.Str.IsSome()) {
                // finally we've got the name!
                string name = nameType.Str.Peel();
            }
        }

        if (obj.ContainsKey("age")) {
            JSONType ageType = obj["age"];
            if (ageType.Num.IsSome()) {
                // note that num is stored as a string
                // the reason for that is I don't know what you need:
                // is it float? is it double? is it int? parse it yourself :)
                // though I gotta say that parser ensures correctness of this
                // string
                string age = ageType.Num.Peel();
            }
        }

        if (obj.ContainsKey("dumb")) {
            JSONType dumbType = obj["dumb"];
            if (dumbType.Bool.IsSome()) {
                bool dumb = dumbType.Bool.Peel();
            }
        }

        if (obj.ContainsKey("credentials")) {
            JSONType credType = obj["credentials"];
            if (credType.Null.IsSome()) {
                // no creds
            }
        }

        if (obj.ContainsKey("repos")) {
            JSONType reposType = obj["repos"];
            if (reposType.Arr.IsSome()) {
                // so array is just a List
                List<JSONType> repos = reposType.Arr.Peel();
            }
        }
    }
}
```
To generate JSON you need to fill **JSONType**
```csharp
Dictionary<string, JSONType> root = new Dictionary<string, JSONType>();
root["foo"] = JSONType.Make("bar");
JSONType type = JSONType.Make(root);

string json = VJP.Generate(type); // {"foo":"bar"}
```
I hope you understand why it is called verbose now.
