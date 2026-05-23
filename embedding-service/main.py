import os
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

MODEL_NAME = os.getenv("EMBEDDING_MODEL", "BAAI/bge-m3")
DIMENSIONS = int(os.getenv("EMBEDDING_DIMENSIONS", "1024"))

model: SentenceTransformer | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global model
    logger.info("Loading model: %s", MODEL_NAME)
    model = SentenceTransformer(MODEL_NAME)
    logger.info("Model loaded, dimension: %d", model.get_sentence_embedding_dimension())
    yield
    model = None


app = FastAPI(title="Embedding Service", lifespan=lifespan)


class EmbeddingRequest(BaseModel):
    model: str = ""
    prompt: str


class EmbeddingResponse(BaseModel):
    embedding: list[float]


@app.post("/api/embeddings")
async def create_embedding(request: EmbeddingRequest) -> EmbeddingResponse:
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    if not request.prompt:
        raise HTTPException(status_code=400, detail="prompt is required")

    embedding = model.encode(request.prompt, normalize_embeddings=True)
    return EmbeddingResponse(embedding=embedding.tolist())


@app.get("/health")
async def health():
    return {"status": "ok", "model": MODEL_NAME}


@app.get("/")
async def root():
    return {
        "service": "embedding-service",
        "model": MODEL_NAME,
        "dimensions": DIMENSIONS,
    }
