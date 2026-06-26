"""UltronTraining tests."""

from ultrontraining.config import TrainingConfig
from ultrontraining.data.loader import DatasetLoader
from ultrontraining.data.features import build_feature_matrix, validate_features
from ultrontraining.pipeline.training_pipeline import TrainingPipeline
from ultrontraining.split.temporal import temporal_split


def test_loader(datasets_dir):
    bundle = DatasetLoader(datasets_dir).load()
    assert len(bundle.df) == 150


def test_temporal_split_no_shuffle(datasets_dir):
    fm = build_feature_matrix(DatasetLoader(datasets_dir).load().df)
    split = temporal_split(fm)
    assert len(split.train.X) + len(split.validation.X) + len(split.test.X) == len(fm.X)
    assert split.train_indices.max() < split.val_indices.min()


def test_full_pipeline_fast(datasets_dir, tmp_path):
    result = TrainingPipeline(
        TrainingConfig(
            datasets_dir=datasets_dir,
            output_dir=tmp_path / "out",
            min_rows=80,
            model_names=["LogisticRegression", "RandomForest"],
            run_shap=False,
            run_walk_forward=True,
            walk_forward_folds=2,
        )
    ).run()

    assert result.champion["name"] in ("LogisticRegression", "RandomForest")
    assert (tmp_path / "out" / "champion" / "model.onnx").exists()
    assert (tmp_path / "out" / "champion" / "feature_list.json").exists()
    assert (tmp_path / "out" / "dashboard" / "index.html").exists()
