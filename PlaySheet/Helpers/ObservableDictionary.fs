namespace PlaySheet.Helpers

open System.ComponentModel
open System.Collections
open System.Collections.Generic
open System.Collections.Specialized
open System.Diagnostics


/// Represents a `Dictionary` that triggers events when it is modified.
[<DebuggerDisplay("Count={Count}")>]
type ObservableDictionary<'K, 'V when 'K : equality> private(dic: IDictionary<'K, 'V>) =

    let collectionChanged = new Event<_, _>()
    let propertyChanged = new Event<_, _>()

    new() = ObservableDictionary(Dictionary())


    interface INotifyCollectionChanged with
        [<CLIEvent>]
        member __.CollectionChanged = collectionChanged.Publish
    
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish
    
    interface IDictionary<'K, 'V> with
        member this.Add(k, v) =
            dic.Add(k, v)

            collectionChanged.Trigger(this,
                NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    KeyValuePair(k, v)))
            
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Count")
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Keys")
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Values")

        member this.Remove(k) =
            match dic.TryGetValue(k) with
            | false, _    -> false
            | true, value ->
                assert dic.Remove(k)

                collectionChanged.Trigger(this,
                    NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove,
                        KeyValuePair(k, value)))
            
                propertyChanged.Trigger(this, PropertyChangedEventArgs "Count")
                propertyChanged.Trigger(this, PropertyChangedEventArgs "Keys")
                propertyChanged.Trigger(this, PropertyChangedEventArgs "Values")

                true
        
        member this.Item
            with get(k) = dic.[k]
             and set(k) (v) =
                match dic.TryGetValue(k) with
                | false, _    -> (this :> IDictionary<_, _>).Add(k, v)
                | true, value ->
                    dic.[k] <- v

                    collectionChanged.Trigger(this,
                        NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Replace,
                            KeyValuePair(k, v),
                            KeyValuePair(k, value)))

                    propertyChanged.Trigger(this, PropertyChangedEventArgs "Values")

        member __.Keys = dic.Keys :> _
        member __.Values = dic.Values :> _

        member __.ContainsKey(k) = dic.ContainsKey(k)
        member __.TryGetValue(k, v) = dic.TryGetValue(k, &v)


    interface ICollection<KeyValuePair<'K, 'V>> with
        member this.Add(pair) = (this :> IDictionary<_, _>).Add(pair.Key, pair.Value)
        member this.Remove(pair) = (this :> IDictionary<_, _>).Remove(pair.Key)

        member __.Contains(pair) = (dic :> ICollection<_>).Contains(pair)
        member __.CopyTo(array, index) = (dic :> ICollection<_>).CopyTo(array, index)
        member __.Count = dic.Count
        member __.IsReadOnly = (dic :> ICollection<_>).IsReadOnly


        member this.Clear() =
            dic.Clear()

            collectionChanged.Trigger(this,
                NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Reset))
        
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Count")
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Keys")
            propertyChanged.Trigger(this, PropertyChangedEventArgs "Values")


    interface IEnumerable<KeyValuePair<'K, 'V>> with
        member __.GetEnumerator() = (dic :> IEnumerable<_>).GetEnumerator()
    
    interface IEnumerable with
        member __.GetEnumerator() = (dic :> IEnumerable).GetEnumerator()
    

    static member Of(pairs: ('K * 'V) seq) =
        let dic = Dictionary()

        for k, v in pairs do
            dic.Add(k, v)
        
        ObservableDictionary dic
    
    static member From(pairs: IEnumerable<KeyValuePair<'K, 'V>>) =
        let dic = Dictionary()

        for pair in pairs do
            (dic :> ICollection<_>).Add(pair)

        ObservableDictionary dic
    
    static member From(dic: IDictionary<'K, 'V>) =
        ObservableDictionary <| Dictionary(dic)
    