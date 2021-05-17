namespace vjp {
    public struct Result<T, E> {
        private Option<T> ok;
        private Option<E> err;

        public T AsOk() {
            return ok.Peel();
        }

        public E AsErr() {
            return err.Peel();
        }

        public bool IsOk() {
            return ok.IsSome();
        }

        public bool IsErr() {
            return err.IsSome();
        }

        public static Result<T, E> Ok(T ok) {
            Result<T, E> result = new Result<T, E>();
            result.ok = Option<T>.Some(ok);
            return result;
        }

        public static Result<T, E> Err(E err) {
            Result<T, E> result = new Result<T, E>();
            result.err = Option<E>.Some(err);
            return result;
        }
    }
}
