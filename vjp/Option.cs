namespace vjp {
    public struct Option<T> {
        private T value;
        private bool filled;

        private Option(T Value, bool filled) {
            this.value = Value;
            this.filled = filled;
        }

        public T Peel() {
            return value;
        }

        public bool IsSome() {
            return filled;
        }

        public bool IsNone() {
            return !filled;
        }

        public static Option<T> Some(T val) {
            return new Option<T>(val, true);
        }

        public static Option<T> None() {
            return new Option<T>(default(T), false);
        }
    }
}
