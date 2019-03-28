namespace SizeScanner.Model {
    public interface IProgressIndicatorService {
        void Begin();
        void End();
        void SetProgress(double value);
    }
}