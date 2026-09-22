import hmac
import os

from fastapi import Header, HTTPException


def require_reward_authority(x_opengarrison_reward_key: str = Header(default="")) -> None:
    """Player authentication identifies recipients; it does not authorize awards."""
    expected = os.environ.get("OPENGARRISON_REWARD_AUTHORITY_KEY", "")
    if not expected or not x_opengarrison_reward_key or not hmac.compare_digest(
        expected.encode(), x_opengarrison_reward_key.encode()
    ):
        raise HTTPException(status_code=403, detail="A trusted reward authority is required")
